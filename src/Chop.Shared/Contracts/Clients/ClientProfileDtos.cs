namespace Chop.Shared.Contracts.Clients;

public sealed class ClientProfileDto
{
    public string UserId { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public IReadOnlyCollection<ClientProfilePhoneDto> Phones { get; set; } = [];

    public IReadOnlyCollection<ClientProfileAddressDto> Addresses { get; set; } = [];
}

public sealed class ClientProfilePhoneDto
{
    public string Phone { get; set; } = string.Empty;

    public string Type { get; set; } = "PRIMARY";

    public bool IsPrimary { get; set; }
}

public sealed class ClientProfileAddressDto
{
    public string Label { get; set; } = string.Empty;

    public string AddressText { get; set; } = string.Empty;

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public bool IsPrimary { get; set; }
}

public sealed class UpdateClientProfileDto
{
    public string FullName { get; set; } = string.Empty;

    public string? Email { get; set; }

    public IReadOnlyCollection<UpdateClientPhoneDto> Phones { get; set; } = [];

    public IReadOnlyCollection<UpdateClientAddressDto> Addresses { get; set; } = [];
}

public sealed class UpdateClientPhoneDto
{
    public string Phone { get; set; } = string.Empty;

    public string Type { get; set; } = "OTHER";

    public bool IsPrimary { get; set; }
}

public sealed class UpdateClientAddressDto
{
    public string Label { get; set; } = string.Empty;

    public string AddressText { get; set; } = string.Empty;

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public bool IsPrimary { get; set; }
}
