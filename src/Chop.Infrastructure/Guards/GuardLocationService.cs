using Chop.Application.Guards;
using Chop.Domain.Geo;
using Chop.Domain.Guards;
using Chop.Infrastructure.Persistence;
using Chop.Shared.Contracts.Guards;
using Microsoft.EntityFrameworkCore;

namespace Chop.Infrastructure.Guards;

public sealed class GuardLocationService : IGuardLocationService
{
    private readonly AppDbContext _dbContext;
    private static readonly TimeSpan PublishThrottle = TimeSpan.FromSeconds(2);

    public GuardLocationService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GuardLocationPingResult> PingAsync(string guardUserId, GuardLocationPingDto request, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.GuardLocations
            .SingleOrDefaultAsync(x => x.GuardUserId == guardUserId, cancellationToken);

        var now = DateTime.UtcNow;
        var shouldPublish = existing is null
            || existing.IncidentId != request.IncidentId
            || (now - existing.UpdatedAtUtc) >= PublishThrottle;

        if (existing is null)
        {
            existing = new GuardLocation
            {
                GuardUserId = guardUserId,
            };
            _dbContext.GuardLocations.Add(existing);
        }

        existing.IncidentId = request.IncidentId;
        existing.Latitude = request.Lat;
        existing.Longitude = request.Lon;
        existing.GeoPoint = GeoPointHelper.Create(request.Lat, request.Lon);
        existing.AccuracyMeters = request.AccuracyM;
        existing.DeviceTimeUtc = request.DeviceTimeUtc;
        existing.ShiftId = request.ShiftId;
        existing.UpdatedAtUtc = now;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new GuardLocationPingResult
        {
            ShouldPublishRealtime = shouldPublish,
        };
    }
}
