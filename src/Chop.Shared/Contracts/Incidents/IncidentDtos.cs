namespace Chop.Shared.Contracts.Incidents;

public sealed class IncidentDto
{
    public Guid Id { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public IncidentLocationDto? Location { get; set; }

    public string? AddressSnapshot { get; set; }

    public string ClientSummary { get; set; } = string.Empty;

    public DateTime LastUpdatedAt { get; set; }
}

public sealed class CreateIncidentResponseDto
{
    public Guid IncidentId { get; set; }

    public IncidentDto Incident { get; set; } = new();
}

public sealed class IncidentListItemDto
{
    public Guid Id { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public string ClientSummary { get; set; } = string.Empty;

    public string? AddressSnapshot { get; set; }

    public DateTime LastUpdatedAt { get; set; }
}

public sealed class IncidentHistoryItemDto
{
    public string? FromStatus { get; set; }

    public string ToStatus { get; set; } = string.Empty;

    public string ActorUserId { get; set; } = string.Empty;

    public string ActorRole { get; set; } = string.Empty;

    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; }
}

public sealed class IncidentDetailsDto
{
    public Guid Id { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public IncidentLocationDto? Location { get; set; }

    public string? AddressSnapshot { get; set; }

    public ClientSummaryDto Client { get; set; } = new();

    public IReadOnlyCollection<ClientAddressDto> Addresses { get; set; } = [];

    public DateTime LastUpdatedAt { get; set; }

    public IReadOnlyCollection<IncidentHistoryItemDto> History { get; set; } = [];

    // Dispatches created for this incident (operator view).
    public IReadOnlyCollection<DispatchDto> Dispatches { get; set; } = [];
}

public sealed class ClientSummaryDto
{
    public string FullName { get; set; } = string.Empty;

    public IReadOnlyCollection<ClientPhoneDto> Phones { get; set; } = [];
}

public sealed class ClientPhoneDto
{
    public string Phone { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public bool IsPrimary { get; set; }
}

public sealed class ClientAddressDto
{
    public string Label { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public IncidentLocationDto? Location { get; set; }

    public bool IsPrimary { get; set; }
}

public sealed class NearestForcesDto
{
    public IReadOnlyCollection<NearestPostDto> Posts { get; set; } = [];

    public IReadOnlyCollection<NearestPatrolUnitDto> PatrolUnits { get; set; } = [];
}

public sealed class NearestPostDto
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public double DistanceMeters { get; set; }

    public string? Phone { get; set; }

    public string? RadioChannel { get; set; }

    public IReadOnlyCollection<string> Responsibles { get; set; } = [];
}

public sealed class NearestPatrolUnitDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public double DistanceMeters { get; set; }

    public string? Phone { get; set; }

    public string? RadioChannel { get; set; }

    public int? LastLocationAgeSeconds { get; set; }
}
