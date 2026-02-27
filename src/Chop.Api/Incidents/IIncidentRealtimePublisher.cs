using Chop.Shared.Contracts.Incidents;

namespace Chop.Api.Incidents;

public interface IIncidentRealtimePublisher
{
    Task PublishIncidentCreatedAsync(IncidentDto incident, CancellationToken cancellationToken);

    Task PublishIncidentStatusChangedAsync(
        IncidentDto incident,
        string fromStatus,
        string toStatus,
        string actorUserId,
        string actorRole,
        string? comment,
        CancellationToken cancellationToken);

    Task PublishDispatchAcceptedAsync(Guid incidentId, string guardUserId, string? comment, CancellationToken cancellationToken);

    Task PublishDispatchCreatedAsync(Guid incidentId, CancellationToken cancellationToken);

    Task PublishGuardLocationUpdatedAsync(Guid? incidentId, string guardUserId, IncidentLocationDto location, CancellationToken cancellationToken);
}
