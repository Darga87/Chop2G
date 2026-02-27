namespace Chop.Domain.Alerts;

public sealed class NotificationOutbox
{
    public Guid Id { get; set; }

    public NotificationChannel Channel { get; set; }

    public string Destination { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = "{}";

    public NotificationOutboxStatus Status { get; set; } = NotificationOutboxStatus.Pending;

    public int AttemptCount { get; set; }

    public DateTime? NextAttemptAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public ICollection<NotificationDelivery> Deliveries { get; set; } = [];
}
