namespace Chop.Domain.Alerts;

public enum NotificationOutboxStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
}
