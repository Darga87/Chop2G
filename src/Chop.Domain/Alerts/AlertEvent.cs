namespace Chop.Domain.Alerts;

public sealed class AlertEvent
{
    public Guid Id { get; set; }

    public string RuleCode { get; set; } = string.Empty;

    public AlertSeverity Severity { get; set; }

    public AlertEventStatus Status { get; set; } = AlertEventStatus.Open;

    public AlertEntityType EntityType { get; set; }

    public Guid? EntityId { get; set; }

    public string PayloadJson { get; set; } = "{}";

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? AckedAtUtc { get; set; }

    public string? AckedByUserId { get; set; }

    public DateTime? ResolvedAtUtc { get; set; }

    public string? ResolvedByUserId { get; set; }
}
