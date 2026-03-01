using Chop.Application.Alerts;
using Chop.Application.Platform;
using Chop.Infrastructure.Persistence;
using Chop.Shared.Contracts.Incidents;
using Chop.Shared.Contracts.Realtime;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Chop.Api.Incidents;

public sealed class IncidentRealtimePublisher : IIncidentRealtimePublisher
{
    private readonly IAlertNotificationService _alertNotificationService;
    private readonly IPlatformOutboxService _platformOutboxService;
    private readonly AppDbContext _dbContext;

    public IncidentRealtimePublisher(
        IAlertNotificationService alertNotificationService,
        IPlatformOutboxService platformOutboxService,
        AppDbContext dbContext)
    {
        _alertNotificationService = alertNotificationService;
        _platformOutboxService = platformOutboxService;
        _dbContext = dbContext;
    }

    public async Task PublishIncidentCreatedAsync(IncidentDto incident, CancellationToken cancellationToken)
    {
        var scope = await BuildScopeByIncidentIdAsync(incident.Id, cancellationToken);
        var payload = new IncidentCreatedEvent
        {
            EventId = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            Incident = incident,
            Scope = scope,
        };

        await PublishWithOutboxAsync(
            () => _alertNotificationService.RecordIncidentCreatedAsync(payload, cancellationToken),
            () => _platformOutboxService.EnqueueAsync("incident", incident.Id, "realtime.incident-created", JsonSerializer.Serialize(payload), cancellationToken));
    }

    public async Task PublishIncidentStatusChangedAsync(
        IncidentDto incident,
        string fromStatus,
        string toStatus,
        string actorUserId,
        string actorRole,
        string? comment,
        CancellationToken cancellationToken)
    {
        var scope = await BuildScopeByIncidentIdAsync(incident.Id, cancellationToken);
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
            Scope = scope,
        };

        await PublishWithOutboxAsync(
            () => _alertNotificationService.RecordIncidentStatusChangedAsync(payload, cancellationToken),
            () => _platformOutboxService.EnqueueAsync("incident", incident.Id, "realtime.incident-status-changed", JsonSerializer.Serialize(payload), cancellationToken));
    }

    public async Task PublishDispatchAcceptedAsync(Guid incidentId, string guardUserId, string? comment, CancellationToken cancellationToken)
    {
        var scope = await BuildScopeByIncidentIdAsync(incidentId, cancellationToken);
        var payload = new DispatchAcceptedEvent
        {
            EventId = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            IncidentId = incidentId,
            GuardUserId = guardUserId,
            Comment = comment,
            Scope = scope,
        };

        await PublishWithOutboxAsync(
            () => _alertNotificationService.RecordDispatchAcceptedAsync(payload, cancellationToken),
            () => _platformOutboxService.EnqueueAsync("incident", incidentId, "realtime.dispatch-accepted", JsonSerializer.Serialize(payload), cancellationToken));
    }

    public async Task PublishDispatchCreatedAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        var scope = await BuildScopeByIncidentIdAsync(incidentId, cancellationToken);
        var payload = new DispatchCreatedEvent
        {
            EventId = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            IncidentId = incidentId,
            Scope = scope,
        };

        await PublishWithOutboxAsync(
            () => _alertNotificationService.RecordDispatchCreatedAsync(payload, cancellationToken),
            () => _platformOutboxService.EnqueueAsync("incident", incidentId, "realtime.dispatch-created", JsonSerializer.Serialize(payload), cancellationToken));
    }

    public async Task PublishGuardLocationUpdatedAsync(Guid? incidentId, string guardUserId, IncidentLocationDto location, CancellationToken cancellationToken)
    {
        if (incidentId is null)
        {
            // Pings without an active incident are stored, but not broadcast to operators.
            return;
        }

        var scope = await BuildScopeByIncidentIdAsync(incidentId.Value, cancellationToken);
        var payload = new GuardLocationUpdatedEvent
        {
            EventId = Guid.NewGuid(),
            OccurredAtUtc = DateTime.UtcNow,
            IncidentId = incidentId,
            GuardUserId = guardUserId,
            Location = location,
            Scope = scope,
        };

        await PublishWithOutboxAsync(
            () => Task.CompletedTask,
            () => _platformOutboxService.EnqueueAsync("incident", incidentId.Value, "realtime.guard-location-updated", JsonSerializer.Serialize(payload), cancellationToken));
    }

    private async Task<RealtimeScopeDto> BuildScopeByIncidentIdAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        var clientUserId = await _dbContext.Incidents
            .Where(x => x.Id == incidentId)
            .Select(x => x.ClientUserId)
            .SingleOrDefaultAsync(cancellationToken);

        return new RealtimeScopeDto
        {
            IncidentId = incidentId,
            ClientUserId = string.IsNullOrWhiteSpace(clientUserId) ? null : clientUserId,
        };
    }

    private static async Task PublishWithOutboxAsync(
        Func<Task> persistTaskFactory,
        Func<Task> platformOutboxTaskFactory)
    {
        await persistTaskFactory();
        await platformOutboxTaskFactory();
    }
}
