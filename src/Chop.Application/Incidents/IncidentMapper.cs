using Chop.Domain.Incidents;
using Chop.Domain.Geo;
using Chop.Shared.Contracts.Incidents;

namespace Chop.Application.Incidents;

internal static class IncidentMapper
{
    public static string ToApiStatus(this IncidentStatus status) =>
        status switch
        {
            IncidentStatus.New => "NEW",
            IncidentStatus.Acked => "ACKED",
            IncidentStatus.Dispatched => "DISPATCHED",
            IncidentStatus.Accepted => "ACCEPTED",
            IncidentStatus.EnRoute => "EN_ROUTE",
            IncidentStatus.OnScene => "ON_SCENE",
            IncidentStatus.Resolved => "RESOLVED",
            IncidentStatus.Canceled => "CANCELED",
            IncidentStatus.FalseAlarm => "FALSE_ALARM",
            IncidentStatus.Failed => "FAILED",
            _ => "NEW",
        };

    public static IncidentDto ToIncidentDto(this Incident incident) =>
        new()
        {
            Id = incident.Id,
            Status = incident.Status.ToApiStatus(),
            CreatedAt = incident.CreatedAtUtc,
            Location = ToLocation(incident.GeoPoint, incident.AccuracyM),
            AddressSnapshot = incident.AddressText,
            ClientSummary = incident.ClientUserId,
            LastUpdatedAt = incident.LastUpdatedAtUtc,
        };

    public static IncidentListItemDto ToListItemDto(this Incident incident) =>
        new()
        {
            Id = incident.Id,
            Status = incident.Status.ToApiStatus(),
            CreatedAt = incident.CreatedAtUtc,
            ClientSummary = incident.ClientUserId,
            AddressSnapshot = incident.AddressText,
            LastUpdatedAt = incident.LastUpdatedAtUtc,
        };

    public static string ToApiMethod(this DispatchMethod method) =>
        method switch
        {
            DispatchMethod.Radio => "RADIO",
            DispatchMethod.Phone => "PHONE",
            DispatchMethod.App => "APP",
            DispatchMethod.Mixed => "MIXED",
            _ => "APP",
        };

    public static string ToApiRecipientType(this DispatchRecipientType type) =>
        type switch
        {
            DispatchRecipientType.Post => "POST",
            DispatchRecipientType.PatrolUnit => "PATROL_UNIT",
            DispatchRecipientType.Guard => "GUARD",
            _ => "GUARD",
        };

    public static string ToApiRecipientStatus(this DispatchRecipientStatus status) =>
        status switch
        {
            DispatchRecipientStatus.Sent => "SENT",
            DispatchRecipientStatus.Accepted => "ACCEPTED",
            DispatchRecipientStatus.Declined => "DECLINED",
            _ => "SENT",
        };

    public static string ToApiAcceptanceVia(this DispatchAcceptanceVia via) =>
        via switch
        {
            DispatchAcceptanceVia.Radio => "RADIO",
            DispatchAcceptanceVia.Phone => "PHONE",
            DispatchAcceptanceVia.App => "APP",
            _ => "APP",
        };

    public static DispatchDto ToDispatchDto(this Dispatch dispatch) =>
        new()
        {
            Id = dispatch.Id,
            IncidentId = dispatch.IncidentId,
            Method = dispatch.Method.ToApiMethod(),
            Comment = dispatch.Comment,
            CreatedByUserId = dispatch.CreatedByUserId,
            CreatedAt = dispatch.CreatedAtUtc,
            Recipients = dispatch.Recipients
                .Select(x => new DispatchRecipientDto
                {
                    Id = x.Id,
                    Type = x.RecipientType.ToApiRecipientType(),
                    RecipientId = x.RecipientId,
                    DistanceMeters = x.DistanceMeters,
                    Status = x.Status.ToApiRecipientStatus(),
                    AcceptedBy = x.AcceptedBy,
                    AcceptedAt = x.AcceptedAtUtc,
                    AcceptedVia = x.AcceptedVia.HasValue ? x.AcceptedVia.Value.ToApiAcceptanceVia() : null,
                })
                .ToArray(),
        };

    private static IncidentLocationDto? ToLocation(NetTopologySuite.Geometries.Point? point, double? accuracyM)
    {
        var (lat, lon) = GeoPointHelper.Read(point);
        if (!lat.HasValue || !lon.HasValue)
        {
            return null;
        }

        return new IncidentLocationDto
        {
            Lat = lat.Value,
            Lon = lon.Value,
            AccuracyM = accuracyM,
        };
    }
}
