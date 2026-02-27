using NetTopologySuite.Geometries;

namespace Chop.Domain.Clients;

public sealed class ClientAddress
{
    public Guid Id { get; set; }

    public Guid ClientProfileId { get; set; }

    public ClientProfile ClientProfile { get; set; } = null!;

    public string Label { get; set; } = string.Empty;

    public string AddressText { get; set; } = string.Empty;

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public Point? GeoPoint { get; set; }

    public bool IsPrimary { get; set; }
}
