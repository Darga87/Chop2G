namespace Chop.Domain.Incidents;

public sealed class Dispatch
{
    public Guid Id { get; set; }

    public Guid IncidentId { get; set; }

    public Incident? Incident { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    public DispatchMethod Method { get; set; }

    public string? Comment { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public ICollection<DispatchRecipient> Recipients { get; set; } = [];
}
