using Chop.Application.Incidents;
using Chop.Domain.Geo;
using Chop.Domain.Incidents;
using Chop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace Chop.Infrastructure.Incidents;

public sealed class IncidentRepository : IIncidentRepository
{
    private readonly AppDbContext _dbContext;

    public IncidentRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddAsync(Incident incident, CancellationToken cancellationToken) =>
        _dbContext.Incidents.AddAsync(incident, cancellationToken).AsTask();

    public Task AddStatusHistoryAsync(IncidentStatusHistory historyItem, CancellationToken cancellationToken) =>
        _dbContext.IncidentStatusHistory.AddAsync(historyItem, cancellationToken).AsTask();

    public Task AddDispatchAsync(Dispatch dispatch, CancellationToken cancellationToken) =>
        _dbContext.Dispatches.AddAsync(dispatch, cancellationToken).AsTask();

    public Task AddAssignmentAsync(IncidentAssignment assignment, CancellationToken cancellationToken) =>
        _dbContext.IncidentAssignments.AddAsync(assignment, cancellationToken).AsTask();

    public Task<DispatchRecipient?> FindGuardRecipientAsync(Guid incidentId, string guardUserId, CancellationToken cancellationToken) =>
        _dbContext.DispatchRecipients
            .Include(x => x.Dispatch)
            .Where(x => x.Dispatch!.IncidentId == incidentId)
            .Where(x => x.RecipientType == DispatchRecipientType.Guard)
            .Where(x => x.RecipientId == guardUserId)
            .OrderByDescending(x => x.Dispatch!.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<IncidentAssignment?> FindLatestGuardAssignmentAsync(Guid incidentId, string guardUserId, CancellationToken cancellationToken) =>
        _dbContext.IncidentAssignments
            .Where(x => x.IncidentId == incidentId)
            .Where(x => x.GuardUserId == guardUserId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<Incident?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _dbContext.Incidents
            .Include(x => x.StatusHistory)
            .Include(x => x.Dispatches)
                .ThenInclude(x => x.Recipients)
            .Include(x => x.ClientProfile)
                .ThenInclude(x => x!.Phones)
            .Include(x => x.ClientProfile)
                .ThenInclude(x => x!.Addresses)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Incident?> FindRecentActiveAsync(string clientUserId, DateTime fromUtc, CancellationToken cancellationToken)
    {
        var activeStatuses = new[]
        {
            IncidentStatus.New,
            IncidentStatus.Acked,
            IncidentStatus.Dispatched,
            IncidentStatus.Accepted,
            IncidentStatus.EnRoute,
            IncidentStatus.OnScene,
        };

        return _dbContext.Incidents
            .Where(x => x.ClientUserId == clientUserId)
            .Where(x => x.CreatedAtUtc >= fromUtc)
            .Where(x => activeStatuses.Contains(x.Status))
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<NearestPostData>> ListNearestPostsAsync(Guid incidentId, int limit, CancellationToken cancellationToken)
    {
        if (IsPostgres())
        {
            return await ListNearestPostsPostgresAsync(incidentId, limit, cancellationToken);
        }

        return await ListNearestPostsFallbackAsync(incidentId, limit, cancellationToken);
    }

    public async Task<IReadOnlyCollection<NearestPatrolUnitData>> ListNearestPatrolUnitsAsync(Guid incidentId, int limit, CancellationToken cancellationToken)
    {
        if (IsPostgres())
        {
            return await ListNearestPatrolUnitsPostgresAsync(incidentId, limit, cancellationToken);
        }

        return await ListNearestPatrolUnitsFallbackAsync(incidentId, limit, cancellationToken);
    }

    public async Task<(IReadOnlyCollection<Incident> Items, int TotalCount)> ListAsync(
        IncidentStatus? status,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        IQueryable<Incident> query = _dbContext.Incidents.AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (fromUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(x => x.CreatedAtUtc <= toUtc.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    private bool IsPostgres()
    {
        var provider = _dbContext.Database.ProviderName;
        return provider?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
    }

    private async Task<IReadOnlyCollection<NearestPostData>> ListNearestPostsPostgresAsync(Guid incidentId, int limit, CancellationToken cancellationToken)
    {
        var result = new List<NearestPostData>();
        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                a."Id",
                a."Label",
                ST_Distance(inc.i_geo, a."GeoPoint") AS distance_m
            FROM client_addresses a
            CROSS JOIN (
                SELECT i."GeoPoint" AS i_geo
                FROM incidents i
                WHERE i."Id" = @incidentId
            ) inc
            WHERE a."GeoPoint" IS NOT NULL
              AND inc.i_geo IS NOT NULL
            ORDER BY distance_m
            LIMIT @limit;
            """;

        var incidentIdParam = command.CreateParameter();
        incidentIdParam.ParameterName = "@incidentId";
        incidentIdParam.Value = incidentId;
        command.Parameters.Add(incidentIdParam);

        var limitParam = command.CreateParameter();
        limitParam.ParameterName = "@limit";
        limitParam.Value = limit;
        command.Parameters.Add(limitParam);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new NearestPostData
            {
                Id = reader.GetGuid(0),
                Name = reader.IsDBNull(1) ? "Post" : reader.GetString(1),
                DistanceMeters = reader.IsDBNull(2) ? 0 : reader.GetDouble(2),
            });
        }

        return result;
    }

    private async Task<IReadOnlyCollection<NearestPatrolUnitData>> ListNearestPatrolUnitsPostgresAsync(Guid incidentId, int limit, CancellationToken cancellationToken)
    {
        var result = new List<NearestPatrolUnitData>();
        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                g."GuardUserId",
                g."GuardUserId",
                g."UpdatedAtUtc",
                ST_Distance(inc.i_geo, g."GeoPoint") AS distance_m
            FROM guard_locations g
            CROSS JOIN (
                SELECT i."GeoPoint" AS i_geo
                FROM incidents i
                WHERE i."Id" = @incidentId
            ) inc
            WHERE g."GeoPoint" IS NOT NULL
              AND inc.i_geo IS NOT NULL
            ORDER BY distance_m
            LIMIT @limit;
            """;

        var incidentIdParam = command.CreateParameter();
        incidentIdParam.ParameterName = "@incidentId";
        incidentIdParam.Value = incidentId;
        command.Parameters.Add(incidentIdParam);

        var limitParam = command.CreateParameter();
        limitParam.ParameterName = "@limit";
        limitParam.Value = limit;
        command.Parameters.Add(limitParam);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new NearestPatrolUnitData
            {
                Id = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                Name = reader.IsDBNull(1) ? "Guard" : reader.GetString(1),
                LastLocationAtUtc = reader.IsDBNull(2) ? null : reader.GetDateTime(2),
                DistanceMeters = reader.IsDBNull(3) ? 0 : reader.GetDouble(3),
            });
        }

        return result;
    }

    private async Task<IReadOnlyCollection<NearestPostData>> ListNearestPostsFallbackAsync(Guid incidentId, int limit, CancellationToken cancellationToken)
    {
        var incident = await _dbContext.Incidents
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == incidentId, cancellationToken);

        if (incident is null)
        {
            return [];
        }

        var (incidentLat, incidentLon) = GeoPointHelper.Read(incident.GeoPoint);
        if (!incidentLat.HasValue || !incidentLon.HasValue)
        {
            return [];
        }

        var posts = await _dbContext.ClientAddresses
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return posts
            .Select(x =>
            {
                var (postLat, postLon) = GeoPointHelper.Read(x.GeoPoint);
                if (!postLat.HasValue || !postLon.HasValue)
                {
                    return null;
                }

                return new NearestPostData
                {
                    Id = x.Id,
                    Name = string.IsNullOrWhiteSpace(x.Label) ? "Post" : x.Label,
                    DistanceMeters = HaversineMeters(incidentLat.Value, incidentLon.Value, postLat.Value, postLon.Value),
                };
            })
            .Where(x => x is not null)
            .Select(x => x!)
            .OrderBy(x => x.DistanceMeters)
            .Take(limit)
            .ToArray();
    }

    private async Task<IReadOnlyCollection<NearestPatrolUnitData>> ListNearestPatrolUnitsFallbackAsync(Guid incidentId, int limit, CancellationToken cancellationToken)
    {
        var incident = await _dbContext.Incidents
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == incidentId, cancellationToken);

        if (incident is null)
        {
            return [];
        }

        var (incidentLat, incidentLon) = GeoPointHelper.Read(incident.GeoPoint);
        if (!incidentLat.HasValue || !incidentLon.HasValue)
        {
            return [];
        }

        var guards = await _dbContext.GuardLocations
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return guards
            .Select(x =>
            {
                var (guardLat, guardLon) = GeoPointHelper.Read(x.GeoPoint);
                if (!guardLat.HasValue || !guardLon.HasValue)
                {
                    return null;
                }

                return new NearestPatrolUnitData
                {
                    Id = x.GuardUserId,
                    Name = x.GuardUserId,
                    LastLocationAtUtc = x.UpdatedAtUtc,
                    DistanceMeters = HaversineMeters(incidentLat.Value, incidentLon.Value, guardLat.Value, guardLon.Value),
                };
            })
            .Where(x => x is not null)
            .Select(x => x!)
            .OrderBy(x => x.DistanceMeters)
            .Take(limit)
            .ToArray();
    }

    private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadius = 6371000;
        static double ToRad(double deg) => deg * Math.PI / 180.0;
        var dLat = ToRad(lat2 - lat1);
        var dLon = ToRad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
                * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadius * c;
    }
}
