namespace Chop.Api.Platform;

public sealed class RealtimeBusEnvelope
{
    public string EventType { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = "{}";

    public DateTime PublishedAtUtc { get; set; } = DateTime.UtcNow;
}
