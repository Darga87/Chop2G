namespace Chop.Shared.Contracts.Guards;

public sealed class GuardLocationPingDto
{
    public double Lat { get; set; }

    public double Lon { get; set; }

    public double? AccuracyM { get; set; }

    public DateTime? DeviceTimeUtc { get; set; }

    public string? ShiftId { get; set; }

    public Guid? IncidentId { get; set; }
}

