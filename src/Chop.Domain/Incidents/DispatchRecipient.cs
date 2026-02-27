namespace Chop.Domain.Incidents;

public sealed class DispatchRecipient
{
    public Guid Id { get; set; }

    public Guid DispatchId { get; set; }

    public Dispatch? Dispatch { get; set; }

    public DispatchRecipientType RecipientType { get; set; }

    public string RecipientId { get; set; } = string.Empty;

    public int? DistanceMeters { get; set; }

    public DispatchRecipientStatus Status { get; set; } = DispatchRecipientStatus.Sent;

    public string? AcceptedBy { get; set; }

    public DateTime? AcceptedAtUtc { get; set; }

    public DispatchAcceptanceVia? AcceptedVia { get; set; }
}
