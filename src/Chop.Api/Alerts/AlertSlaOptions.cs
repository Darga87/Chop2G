namespace Chop.Api.Alerts;

public sealed class AlertSlaOptions
{
    public int PollIntervalMs { get; set; } = 5000;

    public int NoAcceptStuckSeconds { get; set; } = 120;

    public int GuardOfflineSeconds { get; set; } = 120;

    public int StuckInStatusSeconds { get; set; } = 300;
}
