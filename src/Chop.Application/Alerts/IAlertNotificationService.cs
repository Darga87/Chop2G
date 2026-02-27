using Chop.Shared.Contracts.Incidents;
using Chop.Shared.Contracts.Realtime;

namespace Chop.Application.Alerts;

public interface IAlertNotificationService
{
    Task RecordIncidentCreatedAsync(IncidentCreatedEvent payload, CancellationToken cancellationToken);

    Task RecordIncidentStatusChangedAsync(IncidentStatusChangedEvent payload, CancellationToken cancellationToken);

    Task RecordDispatchCreatedAsync(DispatchCreatedEvent payload, CancellationToken cancellationToken);

    Task RecordDispatchAcceptedAsync(DispatchAcceptedEvent payload, CancellationToken cancellationToken);
}
