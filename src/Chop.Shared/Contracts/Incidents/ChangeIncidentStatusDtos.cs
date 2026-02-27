namespace Chop.Shared.Contracts.Incidents;

public sealed class ChangeIncidentStatusDto
{
    public string ToStatus { get; set; } = string.Empty;

    public string? Comment { get; set; }
}

public sealed class GuardAcceptIncidentDto
{
    public string? Comment { get; set; }
}

public sealed class GuardProgressIncidentDto
{
    public string ToStatus { get; set; } = string.Empty;

    public string? Comment { get; set; }
}
