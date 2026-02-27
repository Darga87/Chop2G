namespace Chop.Shared.Contracts.Alerts;

public sealed class AlertListItemDto
{
    public Guid Id { get; set; }

    public string RuleCode { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public string? AssigneeUserId { get; set; }

    public DateTime? AssignedAtUtc { get; set; }
}

public sealed class AckAlertRequestDto
{
    public string? Comment { get; set; }
}

public sealed class ResolveAlertRequestDto
{
    public string? Comment { get; set; }
}

public sealed class AssignAlertRequestDto
{
    public string? AssigneeUserId { get; set; }

    public string? Comment { get; set; }
}

public sealed class OverrideAlertRequestDto
{
    public string Comment { get; set; } = string.Empty;
}
