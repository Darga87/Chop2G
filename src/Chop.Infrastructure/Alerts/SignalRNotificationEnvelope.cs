namespace Chop.Infrastructure.Alerts;

public sealed class SignalRNotificationEnvelope
{
    public string Method { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = "{}";
}
