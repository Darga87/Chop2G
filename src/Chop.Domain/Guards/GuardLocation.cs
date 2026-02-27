using NetTopologySuite.Geometries;

namespace Chop.Domain.Guards;

public sealed class GuardLocation
{
    // Guard user id from auth (NameIdentifier claim).
    public string GuardUserId { get; set; } = string.Empty;

    public Guid? IncidentId { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public Point? GeoPoint { get; set; }

    public double? AccuracyMeters { get; set; }

    public DateTime? DeviceTimeUtc { get; set; }

    public string? ShiftId { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
