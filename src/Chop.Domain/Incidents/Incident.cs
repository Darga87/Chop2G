using Chop.Domain.Clients;
using NetTopologySuite.Geometries;

namespace Chop.Domain.Incidents;

public sealed class Incident
{
    public Guid Id { get; set; }

    public string ClientUserId { get; set; } = string.Empty;

    public ClientProfile? ClientProfile { get; set; }

    public IncidentStatus Status { get; set; } = IncidentStatus.New;

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public Point? GeoPoint { get; set; }

    public double? AccuracyM { get; set; }

    public DateTime? DeviceTimeUtc { get; set; }

    public string? AddressText { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime LastUpdatedAtUtc { get; set; }

    public ICollection<IncidentStatusHistory> StatusHistory { get; set; } = new List<IncidentStatusHistory>();

    public ICollection<Dispatch> Dispatches { get; set; } = new List<Dispatch>();

    public ICollection<IncidentAssignment> Assignments { get; set; } = new List<IncidentAssignment>();
}
