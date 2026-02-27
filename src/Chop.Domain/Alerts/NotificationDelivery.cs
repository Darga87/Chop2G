namespace Chop.Domain.Alerts;

public sealed class NotificationDelivery
{
    public Guid Id { get; set; }

    public Guid OutboxId { get; set; }

    public NotificationOutbox? Outbox { get; set; }

    public NotificationDeliveryStatus Status { get; set; }

    public string? ProviderResponse { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
