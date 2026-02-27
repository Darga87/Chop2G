using Chop.Application.Alerts;
using Chop.Application.Platform;
using Chop.Shared.Contracts.Incidents;
using Chop.Shared.Contracts.Realtime;
using System.Text.Json;

namespace Chop.Api.Incidents;

public sealed class IncidentRealtimePublisher : IIncidentRealtimePublisher
{
    private readonly IAlertNotificationService _alertNotificationService;
    private readonly IPlatformOutboxService _platformOutboxService;

    public IncidentRealtimePublisher(
        IAlertNotificationService alertNotificationService,
        IPlatformOutboxService platformOutboxService)
    {
        _alertNotificationService = alertNotificationService;
        _platformOutboxService = platformOutboxService;
    }

    public Task PublishIncidentCreatedAsync(IncidentDto incident, CancellationToken cancellationToken)
    {
        var payload = new IncidentCreatedEvent
        {
            EventId = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            Incident = incident,
        };

        return PublishWithOutboxAsync(
            () => _alertNotificationService.RecordIncidentCreatedAsync(payload, cancellationToken),
            () => _platformOutboxService.EnqueueAsync("incident", incident.Id, "realtime.incident-created", JsonSerializer.Serialize(payload), cancellationToken));
    }

    public Task PublishIncidentStatusChangedAsync(
        IncidentDto incident,
        string fromStatus,
        string toStatus,
        string actorUserId,
        string actorRole,
        string? comment,
        CancellationToken cancellationToken)
    {
        var payload = new IncidentStatusChangedEvent
        {
            EventId = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            IncidentId = incident.Id,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ActorUserId = actorUserId,
            ActorRole = actorRole,
            Comment = comment,
            Incident = incident,
        };

        return PublishWithOutboxAsync(
            () => _alertNotificationService.RecordIncidentStatusChangedAsync(payload, cancellationToken),
            () => _platformOutboxService.EnqueueAsync("incident", incident.Id, "realtime.incident-status-changed", JsonSerializer.Serialize(payload), cancellationToken));
    }

    public Task PublishDispatchAcceptedAsync(Guid incidentId, string guardUserId, string? comment, CancellationToken cancellationToken)
    {
        var payload = new DispatchAcceptedEvent
        {
            EventId = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            IncidentId = incidentId,
            GuardUserId = guardUserId,
            Comment = comment,
        };

        return PublishWithOutboxAsync(
            () => _alertNotificationService.RecordDispatchAcceptedAsync(payload, cancellationToken),
            () => _platformOutboxService.EnqueueAsync("incident", incidentId, "realtime.dispatch-accepted", JsonSerializer.Serialize(payload), cancellationToken));
    }

    public Task PublishDispatchCreatedAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        var payload = new DispatchCreatedEvent
        {
            EventId = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            IncidentId = incidentId,
        };

        return PublishWithOutboxAsync(
            () => _alertNotificationService.RecordDispatchCreatedAsync(payload, cancellationToken),
            () => _platformOutboxService.EnqueueAsync("incident", incidentId, "realtime.dispatch-created", JsonSerializer.Serialize(payload), cancellationToken));
    }

    public Task PublishGuardLocationUpdatedAsync(Guid? incidentId, string guardUserId, IncidentLocationDto location, CancellationToken cancellationToken)
    {
        if (incidentId is null)
        {
            // Pings without an active incident are stored, but not broadcast to operators.
            return Task.CompletedTask;
        }

        var payload = new GuardLocationUpdatedEvent
        {
            EventId = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            IncidentId = incidentId,
            GuardUserId = guardUserId,
            Location = location,
        };

        return PublishWithOutboxAsync(
            () => Task.CompletedTask,
            () => _platformOutboxService.EnqueueAsync("incident", incidentId.Value, "realtime.guard-location-updated", JsonSerializer.Serialize(payload), cancellationToken));
    }

    private static async Task PublishWithOutboxAsync(
        Func<Task> persistTaskFactory,
        Func<Task> platformOutboxTaskFactory)
    {
        await persistTaskFactory();
        await platformOutboxTaskFactory();
    }
}
