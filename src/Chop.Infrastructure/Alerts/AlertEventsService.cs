using System.Text.Json;
using System.Text.Json.Nodes;
using System.Globalization;
using Chop.Application.Alerts;
using Chop.Domain.Alerts;
using Chop.Domain.Geo;
using Chop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Chop.Shared.Contracts.Alerts;

namespace Chop.Infrastructure.Alerts;

public static class AlertRuleCodes
{
    public const string IncidentAlertPointNoGeo = "INCIDENT_ALERT_POINT_NO_GEO";
    public const string IncidentHomePointNoGeo = "INCIDENT_HOME_POINT_NO_GEO";
    public const string IncidentPointConflict = "INCIDENT_POINT_CONFLICT";
    public const string IncidentSecondGroupMissing = "INCIDENT_SECOND_GROUP_MISSING";
    public const string IncidentNoAccept = "INCIDENT_NO_ACCEPT";
    public const string IncidentGuardNoPing = "INCIDENT_GUARD_NO_PING";
    public const string IncidentNoAcceptStuck = "INCIDENT_NO_ACCEPT_STUCK";
    public const string IncidentGuardOffline = "INCIDENT_GUARD_OFFLINE";
    public const string IncidentStuckInStatus = "INCIDENT_STUCK_IN_STATUS";
}

public sealed class AlertEventsService : IAlertEventsService
{
    private const double PointConflictThresholdMeters = 1000d;
    private readonly AppDbContext _dbContext;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public AlertEventsService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task EnsureGeoAlertsForIncidentAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        var incident = await _dbContext.Incidents
            .Include(x => x.ClientProfile)
                .ThenInclude(x => x!.Addresses)
            .SingleOrDefaultAsync(x => x.Id == incidentId, cancellationToken);

        if (incident is null)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var (incidentLat, incidentLon) = GeoPointHelper.Read(incident.GeoPoint);

        // ALERT point geo missing.
        if (!incidentLat.HasValue || !incidentLon.HasValue)
        {
            await EnsureOpenAlertAsync(
                ruleCode: AlertRuleCodes.IncidentAlertPointNoGeo,
                severity: AlertSeverity.Critical,
                entityType: AlertEntityType.Incident,
                entityId: incident.Id,
                payload: new { incidentId = incident.Id },
                createdAtUtc: now,
                cancellationToken);
        }

        // HOME point geo missing (no primary address or no lat/lon on primary address).
        var primary = incident.ClientProfile?.Addresses
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.Label)
            .FirstOrDefault();
        var (homeLat, homeLon) = primary is null
            ? (null, null)
            : GeoPointHelper.Read(primary.GeoPoint);

        if (primary is null || !homeLat.HasValue || !homeLon.HasValue)
        {
            await EnsureOpenAlertAsync(
                ruleCode: AlertRuleCodes.IncidentHomePointNoGeo,
                severity: AlertSeverity.Warn,
                entityType: AlertEntityType.Incident,
                entityId: incident.Id,
                payload: new { incidentId = incident.Id },
                createdAtUtc: now,
                cancellationToken);
        }

        // ALERT vs HOME point conflict: both points have geo but are far apart.
        if (incidentLat.HasValue && incidentLon.HasValue && homeLat.HasValue && homeLon.HasValue)
        {
            var distanceMeters = ComputeDistanceMeters(
                incidentLat.Value,
                incidentLon.Value,
                homeLat.Value,
                homeLon.Value);

            if (distanceMeters >= PointConflictThresholdMeters)
            {
                await EnsureOpenAlertAsync(
                    ruleCode: AlertRuleCodes.IncidentPointConflict,
                    severity: AlertSeverity.Warn,
                    entityType: AlertEntityType.Incident,
                    entityId: incident.Id,
                    payload: new { incidentId = incident.Id, distanceMeters = Math.Round(distanceMeters, 1) },
                    createdAtUtc: now,
                    cancellationToken);
            }
        }
    }

    public async Task EnsureSecondGroupAlertAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        var dispatchCount = await _dbContext.Dispatches
            .Where(x => x.IncidentId == incidentId)
            .CountAsync(cancellationToken);

        if (dispatchCount >= 2)
        {
            // Resolve any existing open/acked alert.
            var existing = await _dbContext.AlertEvents
                .Where(x => x.EntityType == AlertEntityType.Incident)
                .Where(x => x.EntityId == incidentId)
                .Where(x => x.RuleCode == AlertRuleCodes.IncidentSecondGroupMissing)
                .Where(x => x.Status != AlertEventStatus.Resolved)
                .ToArrayAsync(cancellationToken);

            if (existing.Length == 0)
            {
                return;
            }

            var now = DateTime.UtcNow;
            foreach (var a in existing)
            {
                a.Status = AlertEventStatus.Resolved;
                a.ResolvedAtUtc = now;
                a.ResolvedByUserId = "system";
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        if (dispatchCount == 1)
        {
            await EnsureOpenAlertAsync(
                ruleCode: AlertRuleCodes.IncidentSecondGroupMissing,
                severity: AlertSeverity.Warn,
                entityType: AlertEntityType.Incident,
                entityId: incidentId,
                payload: new { incidentId },
                createdAtUtc: DateTime.UtcNow,
                cancellationToken);
        }
    }

    public async Task EnsureNoAcceptAlertAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        // If incident is DISPATCHED and no guard has accepted, show an operator alert.
        var incident = await _dbContext.Incidents
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == incidentId, cancellationToken);
        if (incident is null)
        {
            return;
        }

        if (!string.Equals(incident.Status.ToString(), "Dispatched", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var hasAcceptedGuard = await _dbContext.DispatchRecipients
            .Include(x => x.Dispatch)
            .Where(x => x.Dispatch!.IncidentId == incidentId)
            .Where(x => x.RecipientType == Chop.Domain.Incidents.DispatchRecipientType.Guard)
            .AnyAsync(x => x.Status == Chop.Domain.Incidents.DispatchRecipientStatus.Accepted, cancellationToken);

        if (hasAcceptedGuard)
        {
            return;
        }

        await EnsureOpenAlertAsync(
            ruleCode: AlertRuleCodes.IncidentNoAccept,
            severity: AlertSeverity.Critical,
            entityType: AlertEntityType.Incident,
            entityId: incidentId,
            payload: new { incidentId },
            createdAtUtc: DateTime.UtcNow,
            cancellationToken);
    }

    public async Task ResolveNoAcceptAlertAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.AlertEvents
            .Where(x => x.EntityType == AlertEntityType.Incident)
            .Where(x => x.EntityId == incidentId)
            .Where(x => x.RuleCode == AlertRuleCodes.IncidentNoAccept)
            .Where(x => x.Status != AlertEventStatus.Resolved)
            .ToArrayAsync(cancellationToken);

        if (existing.Length == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var a in existing)
        {
            a.Status = AlertEventStatus.Resolved;
            a.ResolvedAtUtc = now;
            a.ResolvedByUserId = "system";
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task EnsureGuardNoPingAlertAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        // If at least one guard accepted but we haven't received any ping for this incident, alert operators.
        var hasAcceptedGuard = await _dbContext.DispatchRecipients
            .Include(x => x.Dispatch)
            .Where(x => x.Dispatch!.IncidentId == incidentId)
            .Where(x => x.RecipientType == Chop.Domain.Incidents.DispatchRecipientType.Guard)
            .AnyAsync(x => x.Status == Chop.Domain.Incidents.DispatchRecipientStatus.Accepted, cancellationToken);

        if (!hasAcceptedGuard)
        {
            return;
        }

        var hasAnyPingForIncident = await _dbContext.GuardLocations
            .AsNoTracking()
            .AnyAsync(x => x.IncidentId == incidentId, cancellationToken);

        if (hasAnyPingForIncident)
        {
            return;
        }

        await EnsureOpenAlertAsync(
            ruleCode: AlertRuleCodes.IncidentGuardNoPing,
            severity: AlertSeverity.Warn,
            entityType: AlertEntityType.Incident,
            entityId: incidentId,
            payload: new { incidentId },
            createdAtUtc: DateTime.UtcNow,
            cancellationToken);
    }

    public async Task ResolveGuardNoPingAlertAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.AlertEvents
            .Where(x => x.EntityType == AlertEntityType.Incident)
            .Where(x => x.EntityId == incidentId)
            .Where(x => x.RuleCode == AlertRuleCodes.IncidentGuardNoPing)
            .Where(x => x.Status != AlertEventStatus.Resolved)
            .ToArrayAsync(cancellationToken);

        if (existing.Length == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var a in existing)
        {
            a.Status = AlertEventStatus.Resolved;
            a.ResolvedAtUtc = now;
            a.ResolvedByUserId = "system";
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task EnsureNoAcceptStuckAlertAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        await EnsureOpenAlertAsync(
            ruleCode: AlertRuleCodes.IncidentNoAcceptStuck,
            severity: AlertSeverity.Critical,
            entityType: AlertEntityType.Incident,
            entityId: incidentId,
            payload: new { incidentId },
            createdAtUtc: DateTime.UtcNow,
            cancellationToken);
    }

    public async Task ResolveNoAcceptStuckAlertAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.AlertEvents
            .Where(x => x.EntityType == AlertEntityType.Incident)
            .Where(x => x.EntityId == incidentId)
            .Where(x => x.RuleCode == AlertRuleCodes.IncidentNoAcceptStuck)
            .Where(x => x.Status != AlertEventStatus.Resolved)
            .ToArrayAsync(cancellationToken);

        if (existing.Length == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var a in existing)
        {
            a.Status = AlertEventStatus.Resolved;
            a.ResolvedAtUtc = now;
            a.ResolvedByUserId = "system";
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task EnsureGuardOfflineAlertAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        await EnsureOpenAlertAsync(
            ruleCode: AlertRuleCodes.IncidentGuardOffline,
            severity: AlertSeverity.Critical,
            entityType: AlertEntityType.Incident,
            entityId: incidentId,
            payload: new { incidentId },
            createdAtUtc: DateTime.UtcNow,
            cancellationToken);
    }

    public async Task ResolveGuardOfflineAlertAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.AlertEvents
            .Where(x => x.EntityType == AlertEntityType.Incident)
            .Where(x => x.EntityId == incidentId)
            .Where(x => x.RuleCode == AlertRuleCodes.IncidentGuardOffline)
            .Where(x => x.Status != AlertEventStatus.Resolved)
            .ToArrayAsync(cancellationToken);

        if (existing.Length == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var a in existing)
        {
            a.Status = AlertEventStatus.Resolved;
            a.ResolvedAtUtc = now;
            a.ResolvedByUserId = "system";
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task EnsureStuckInStatusAlertAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        await EnsureOpenAlertAsync(
            ruleCode: AlertRuleCodes.IncidentStuckInStatus,
            severity: AlertSeverity.Warn,
            entityType: AlertEntityType.Incident,
            entityId: incidentId,
            payload: new { incidentId },
            createdAtUtc: DateTime.UtcNow,
            cancellationToken);
    }

    public async Task ResolveStuckInStatusAlertAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.AlertEvents
            .Where(x => x.EntityType == AlertEntityType.Incident)
            .Where(x => x.EntityId == incidentId)
            .Where(x => x.RuleCode == AlertRuleCodes.IncidentStuckInStatus)
            .Where(x => x.Status != AlertEventStatus.Resolved)
            .ToArrayAsync(cancellationToken);

        if (existing.Length == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var a in existing)
        {
            a.Status = AlertEventStatus.Resolved;
            a.ResolvedAtUtc = now;
            a.ResolvedByUserId = "system";
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<AlertListItemDto>> ListIncidentAlertsAsync(Guid incidentId, bool includeResolved, CancellationToken cancellationToken)
    {
        var query = _dbContext.AlertEvents.AsNoTracking()
            .Where(x => x.EntityType == AlertEntityType.Incident)
            .Where(x => x.EntityId == incidentId)
            .Where(x => x.RuleCode == AlertRuleCodes.IncidentAlertPointNoGeo
                        || x.RuleCode == AlertRuleCodes.IncidentHomePointNoGeo
                        || x.RuleCode == AlertRuleCodes.IncidentPointConflict
                        || x.RuleCode == AlertRuleCodes.IncidentSecondGroupMissing
                        || x.RuleCode == AlertRuleCodes.IncidentNoAccept
                        || x.RuleCode == AlertRuleCodes.IncidentGuardNoPing
                        || x.RuleCode == AlertRuleCodes.IncidentNoAcceptStuck
                        || x.RuleCode == AlertRuleCodes.IncidentGuardOffline
                        || x.RuleCode == AlertRuleCodes.IncidentStuckInStatus);

        if (!includeResolved)
        {
            query = query.Where(x => x.Status != AlertEventStatus.Resolved);
        }

        var items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToArrayAsync(cancellationToken);

        return items.Select(ToListItem).ToArray();
    }

    public async Task AckAsync(Guid alertId, string actorUserId, string actorRole, string? comment, CancellationToken cancellationToken)
    {
        var alert = await _dbContext.AlertEvents.SingleOrDefaultAsync(x => x.Id == alertId, cancellationToken);
        if (alert is null)
        {
            throw new InvalidOperationException("Alert not found.");
        }

        if (alert.Status == AlertEventStatus.Resolved)
        {
            return;
        }

        alert.Status = AlertEventStatus.Acked;
        alert.AckedAtUtc = DateTime.UtcNow;
        alert.AckedByUserId = actorUserId;

        if (!string.IsNullOrWhiteSpace(comment))
        {
            alert.PayloadJson = MergeComment(alert.PayloadJson, actorRole, actorUserId, comment);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ResolveAsync(Guid alertId, string actorUserId, string actorRole, string? comment, CancellationToken cancellationToken)
    {
        var alert = await _dbContext.AlertEvents.SingleOrDefaultAsync(x => x.Id == alertId, cancellationToken);
        if (alert is null)
        {
            throw new InvalidOperationException("Alert not found.");
        }

        if (alert.Status == AlertEventStatus.Resolved)
        {
            return;
        }

        alert.Status = AlertEventStatus.Resolved;
        alert.ResolvedAtUtc = DateTime.UtcNow;
        alert.ResolvedByUserId = actorUserId;

        if (!string.IsNullOrWhiteSpace(comment))
        {
            alert.PayloadJson = MergeComment(alert.PayloadJson, actorRole, actorUserId, comment);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AssignAsync(Guid alertId, string actorUserId, string actorRole, string assigneeUserId, string? comment, CancellationToken cancellationToken)
    {
        var alert = await _dbContext.AlertEvents.SingleOrDefaultAsync(x => x.Id == alertId, cancellationToken);
        if (alert is null)
        {
            throw new InvalidOperationException("Alert not found.");
        }

        if (alert.Status == AlertEventStatus.Resolved)
        {
            return;
        }

        alert.PayloadJson = MergeAssignment(alert.PayloadJson, actorRole, actorUserId, assigneeUserId, comment);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task OverrideAsync(Guid alertId, string actorUserId, string actorRole, string comment, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            throw new InvalidOperationException("Override comment is required.");
        }

        var alert = await _dbContext.AlertEvents.SingleOrDefaultAsync(x => x.Id == alertId, cancellationToken);
        if (alert is null)
        {
            throw new InvalidOperationException("Alert not found.");
        }

        if (alert.Status == AlertEventStatus.Resolved)
        {
            return;
        }

        alert.Status = AlertEventStatus.Resolved;
        alert.ResolvedAtUtc = DateTime.UtcNow;
        alert.ResolvedByUserId = actorUserId;
        alert.PayloadJson = MergeOverride(alert.PayloadJson, actorRole, actorUserId, comment);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureOpenAlertAsync(
        string ruleCode,
        AlertSeverity severity,
        AlertEntityType entityType,
        Guid entityId,
        object payload,
        DateTime createdAtUtc,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.AlertEvents
            .Where(x => x.EntityType == entityType)
            .Where(x => x.EntityId == entityId)
            .Where(x => x.RuleCode == ruleCode)
            .Where(x => x.Status != AlertEventStatus.Resolved)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            return;
        }

        _dbContext.AlertEvents.Add(new AlertEvent
        {
            Id = Guid.NewGuid(),
            RuleCode = ruleCode,
            Severity = severity,
            Status = AlertEventStatus.Open,
            EntityType = entityType,
            EntityId = entityId,
            PayloadJson = JsonSerializer.Serialize(payload, SerializerOptions),
            CreatedAtUtc = createdAtUtc,
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static AlertListItemDto ToListItem(AlertEvent e) =>
        new()
        {
            Id = e.Id,
            RuleCode = e.RuleCode,
            Severity = e.Severity.ToString().ToUpperInvariant(),
            Status = e.Status.ToString().ToUpperInvariant(),
            Summary = BuildSummary(e.RuleCode),
            CreatedAtUtc = e.CreatedAtUtc,
            AssigneeUserId = ExtractAssigneeUserId(e.PayloadJson),
            AssignedAtUtc = ExtractAssignedAtUtc(e.PayloadJson),
        };

    private static string BuildSummary(string ruleCode) =>
        ruleCode switch
        {
            AlertRuleCodes.IncidentAlertPointNoGeo => "У точки тревоги нет гео-координат (локация инцидента не задана).",
            AlertRuleCodes.IncidentHomePointNoGeo => "У домашней точки нет гео-координат (у основного адреса клиента нет гео).",
            AlertRuleCodes.IncidentPointConflict => "Конфликт точек тревоги и дома (расстояние выше порога).",
            AlertRuleCodes.IncidentSecondGroupMissing => "Не хватает второй группы (создана только 1 диспетчеризация).",
            AlertRuleCodes.IncidentNoAccept => "Ни один охранник пока не принял диспетчеризацию.",
            AlertRuleCodes.IncidentGuardNoPing => "Охранник принял задачу, но гео-пинги ещё не поступили.",
            AlertRuleCodes.IncidentNoAcceptStuck => "Застревание диспетчеризации: никто не принял в SLA-окно.",
            AlertRuleCodes.IncidentGuardOffline => "Охранник оффлайн: нет гео-пинга в SLA-окне.",
            AlertRuleCodes.IncidentStuckInStatus => "Инцидент застрял в статусе EN_ROUTE/ON_SCENE дольше SLA.",
            _ => ruleCode,
        };

    private static string MergeComment(string payloadJson, string actorRole, string actorUserId, string comment)
    {
        var baseObj = ParsePayloadObject(payloadJson);
        baseObj["lastComment"] = new JsonObject
        {
            ["actorRole"] = actorRole,
            ["actorUserId"] = actorUserId,
            ["comment"] = comment,
            ["atUtc"] = DateTime.UtcNow,
        };
        return baseObj.ToJsonString(SerializerOptions);
    }

    private static string MergeAssignment(string payloadJson, string actorRole, string actorUserId, string assigneeUserId, string? comment)
    {
        var baseObj = ParsePayloadObject(payloadJson);
        baseObj["assignment"] = new JsonObject
        {
            ["assigneeUserId"] = assigneeUserId,
            ["assignedByUserId"] = actorUserId,
            ["assignedByRole"] = actorRole,
            ["comment"] = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            ["atUtc"] = DateTime.UtcNow,
        };

        return baseObj.ToJsonString(SerializerOptions);
    }

    private static string MergeOverride(string payloadJson, string actorRole, string actorUserId, string comment)
    {
        var baseObj = ParsePayloadObject(payloadJson);
        baseObj["lastOverride"] = new JsonObject
        {
            ["actorRole"] = actorRole,
            ["actorUserId"] = actorUserId,
            ["comment"] = comment.Trim(),
            ["atUtc"] = DateTime.UtcNow,
        };

        return baseObj.ToJsonString(SerializerOptions);
    }

    private static JsonObject ParsePayloadObject(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return new JsonObject();
        }

        return JsonNode.Parse(payloadJson) as JsonObject ?? new JsonObject();
    }

    private static string? ExtractAssigneeUserId(string payloadJson)
    {
        var payload = ParsePayloadObject(payloadJson);
        return payload["assignment"]?["assigneeUserId"]?.GetValue<string>();
    }

    private static DateTime? ExtractAssignedAtUtc(string payloadJson)
    {
        var payload = ParsePayloadObject(payloadJson);
        var raw = payload["assignment"]?["atUtc"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed) ? parsed : null;
    }

    private static double ComputeDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusMeters = 6371000d;
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);
        var a = Math.Pow(Math.Sin(dLat / 2d), 2d)
                + Math.Cos(DegreesToRadians(lat1))
                * Math.Cos(DegreesToRadians(lat2))
                * Math.Pow(Math.Sin(dLon / 2d), 2d);
        var c = 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));
        return earthRadiusMeters * c;
    }

    private static double DegreesToRadians(double value) => value * (Math.PI / 180d);
}
