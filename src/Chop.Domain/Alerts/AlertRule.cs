namespace Chop.Domain.Alerts;

public sealed class AlertRule
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    public string SettingsJson { get; set; } = "{}";

    public DateTime CreatedAtUtc { get; set; }
}
