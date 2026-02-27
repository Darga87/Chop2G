namespace Chop.Domain.Incidents;

public sealed class IncidentStatusHistory
{
    public Guid Id { get; set; }

    public Guid IncidentId { get; set; }

    public Incident Incident { get; set; } = null!;

    public IncidentStatus? FromStatus { get; set; }

    public IncidentStatus ToStatus { get; set; }

    public string ActorUserId { get; set; } = string.Empty;

    public string ActorRole { get; set; } = string.Empty;

    public string? Comment { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
