using Chop.Domain.Incidents;

namespace Chop.Application.Incidents;

public interface IIncidentRepository
{
    Task AddAsync(Incident incident, CancellationToken cancellationToken);

    Task AddStatusHistoryAsync(IncidentStatusHistory historyItem, CancellationToken cancellationToken);

    Task AddDispatchAsync(Dispatch dispatch, CancellationToken cancellationToken);

    Task AddAssignmentAsync(IncidentAssignment assignment, CancellationToken cancellationToken);

    Task<DispatchRecipient?> FindGuardRecipientAsync(Guid incidentId, string guardUserId, CancellationToken cancellationToken);

    Task<IncidentAssignment?> FindLatestGuardAssignmentAsync(Guid incidentId, string guardUserId, CancellationToken cancellationToken);

    Task<Incident?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<Incident?> FindRecentActiveAsync(string clientUserId, DateTime fromUtc, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<NearestPostData>> ListNearestPostsAsync(Guid incidentId, int limit, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<NearestPatrolUnitData>> ListNearestPatrolUnitsAsync(Guid incidentId, int limit, CancellationToken cancellationToken);

    Task<(IReadOnlyCollection<Incident> Items, int TotalCount)> ListAsync(
        IncidentStatus? status,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}

public sealed class NearestPostData
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public double DistanceMeters { get; set; }
}

public sealed class NearestPatrolUnitData
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public double DistanceMeters { get; set; }

    public DateTime? LastLocationAtUtc { get; set; }
}
