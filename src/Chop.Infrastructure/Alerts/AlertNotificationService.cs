using System.Text.Json;
using Chop.Application.Alerts;
using Chop.Domain.Alerts;
using Chop.Infrastructure.Persistence;
using Chop.Shared.Contracts.Realtime;

namespace Chop.Infrastructure.Alerts;

public sealed class AlertNotificationService : IAlertNotificationService
{
    private readonly AppDbContext _dbContext;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public AlertNotificationService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task RecordIncidentCreatedAsync(IncidentCreatedEvent payload, CancellationToken cancellationToken) =>
        RecordAsync(
            ruleCode: "INCIDENT_CREATED",
            severity: AlertSeverity.Info,
            entityType: AlertEntityType.Incident,
            entityId: payload.Incident.Id,
            payload: payload,
            cancellationToken);

    public Task RecordIncidentStatusChangedAsync(IncidentStatusChangedEvent payload, CancellationToken cancellationToken) =>
        RecordAsync(
            ruleCode: "INCIDENT_STATUS_CHANGED",
            severity: AlertSeverity.Info,
            entityType: AlertEntityType.Incident,
            entityId: payload.IncidentId,
            payload: payload,
            cancellationToken);

    public Task RecordDispatchCreatedAsync(DispatchCreatedEvent payload, CancellationToken cancellationToken) =>
        RecordAsync(
            ruleCode: "DISPATCH_CREATED",
            severity: AlertSeverity.Warn,
            entityType: AlertEntityType.Incident,
            entityId: payload.IncidentId,
            payload: payload,
            cancellationToken);

    public Task RecordDispatchAcceptedAsync(DispatchAcceptedEvent payload, CancellationToken cancellationToken) =>
        RecordAsync(
            ruleCode: "DISPATCH_ACCEPTED",
            severity: AlertSeverity.Info,
            entityType: AlertEntityType.Incident,
            entityId: payload.IncidentId,
            payload: payload,
            cancellationToken);

    private async Task RecordAsync<T>(
        string ruleCode,
        AlertSeverity severity,
        AlertEntityType entityType,
        Guid? entityId,
        T payload,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var payloadJson = JsonSerializer.Serialize(payload, SerializerOptions);

        _dbContext.AlertEvents.Add(new AlertEvent
        {
            Id = Guid.NewGuid(),
            RuleCode = ruleCode,
            Severity = severity,
            Status = AlertEventStatus.Resolved, // These are realtime event logs, not operator-actionable alerts.
            EntityType = entityType,
            EntityId = entityId,
            PayloadJson = payloadJson,
            CreatedAtUtc = now,
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
