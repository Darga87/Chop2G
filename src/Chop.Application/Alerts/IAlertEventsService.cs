using Chop.Shared.Contracts.Alerts;

namespace Chop.Application.Alerts;

public interface IAlertEventsService
{
    Task EnsureGeoAlertsForIncidentAsync(Guid incidentId, CancellationToken cancellationToken);

    Task EnsureSecondGroupAlertAsync(Guid incidentId, CancellationToken cancellationToken);

    Task EnsureNoAcceptAlertAsync(Guid incidentId, CancellationToken cancellationToken);

    Task ResolveNoAcceptAlertAsync(Guid incidentId, CancellationToken cancellationToken);

    Task EnsureGuardNoPingAlertAsync(Guid incidentId, CancellationToken cancellationToken);

    Task ResolveGuardNoPingAlertAsync(Guid incidentId, CancellationToken cancellationToken);

    Task EnsureNoAcceptStuckAlertAsync(Guid incidentId, CancellationToken cancellationToken);

    Task ResolveNoAcceptStuckAlertAsync(Guid incidentId, CancellationToken cancellationToken);

    Task EnsureGuardOfflineAlertAsync(Guid incidentId, CancellationToken cancellationToken);

    Task ResolveGuardOfflineAlertAsync(Guid incidentId, CancellationToken cancellationToken);

    Task EnsureStuckInStatusAlertAsync(Guid incidentId, CancellationToken cancellationToken);

    Task ResolveStuckInStatusAlertAsync(Guid incidentId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<AlertListItemDto>> ListIncidentAlertsAsync(Guid incidentId, bool includeResolved, CancellationToken cancellationToken);

    Task AckAsync(Guid alertId, string actorUserId, string actorRole, string? comment, CancellationToken cancellationToken);

    Task ResolveAsync(Guid alertId, string actorUserId, string actorRole, string? comment, CancellationToken cancellationToken);

    Task AssignAsync(Guid alertId, string actorUserId, string actorRole, string assigneeUserId, string? comment, CancellationToken cancellationToken);

    Task OverrideAsync(Guid alertId, string actorUserId, string actorRole, string comment, CancellationToken cancellationToken);
}
