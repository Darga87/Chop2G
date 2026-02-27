namespace Chop.Domain.Incidents;

public sealed class IncidentAssignment
{
    public Guid Id { get; set; }

    public Guid IncidentId { get; set; }

    public Incident? Incident { get; set; }

    public string? GuardUserId { get; set; }

    public string? PatrolUnitId { get; set; }

    public IncidentAssignmentStatus Status { get; set; } = IncidentAssignmentStatus.Assigned;

    public DateTime CreatedAtUtc { get; set; }
}
