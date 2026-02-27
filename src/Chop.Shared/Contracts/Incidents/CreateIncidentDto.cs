namespace Chop.Shared.Contracts.Incidents;

public sealed class CreateIncidentDto
{
    public IncidentLocationDto? ClientLocation { get; set; }

    public DateTime? DeviceTimeUtc { get; set; }

    public string? AddressText { get; set; }
}

public sealed class IncidentLocationDto
{
    public double Lat { get; set; }

    public double Lon { get; set; }

    public double? AccuracyM { get; set; }
}
