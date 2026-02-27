namespace Chop.Shared.Contracts.Incidents;

public sealed class CreateDispatchDto
{
    public string Method { get; set; } = string.Empty;

    public IReadOnlyCollection<DispatchRecipientInputDto> Recipients { get; set; } = [];

    public string? Comment { get; set; }
}

public sealed class DispatchRecipientInputDto
{
    public string Type { get; set; } = string.Empty;

    public string Id { get; set; } = string.Empty;

    public int? DistanceMeters { get; set; }
}

public sealed class DispatchDto
{
    public Guid Id { get; set; }

    public Guid IncidentId { get; set; }

    public string Method { get; set; } = string.Empty;

    public string? Comment { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public IReadOnlyCollection<DispatchRecipientDto> Recipients { get; set; } = [];
}

public sealed class DispatchRecipientDto
{
    public Guid Id { get; set; }

    public string Type { get; set; } = string.Empty;

    public string RecipientId { get; set; } = string.Empty;

    public int? DistanceMeters { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? AcceptedBy { get; set; }

    public DateTime? AcceptedAt { get; set; }

    public string? AcceptedVia { get; set; }
}
