using System.Text.Json;
using Chop.Shared.Contracts.Realtime;

namespace Chop.Api.Platform;

internal static class RealtimeRouting
{
    public static string ResolveMethod(string eventType) =>
        eventType switch
        {
            "realtime.incident-created" => "IncidentCreated",
            "realtime.incident-status-changed" => "IncidentStatusChanged",
            "realtime.dispatch-created" => "DispatchCreated",
            "realtime.dispatch-accepted" => "DispatchAccepted",
            "realtime.guard-location-updated" => "GuardLocationUpdated",
            _ => throw new InvalidOperationException($"Unsupported outbox event type '{eventType}'."),
        };

    public static IReadOnlyCollection<string> ResolveDestinations(JsonElement payload)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            IncidentRealtimeGroups.AdminRole,
            IncidentRealtimeGroups.SuperAdminRole,
        };

        if (TryReadScope(payload, out var scope))
        {
            if (scope.IncidentId is Guid incidentId)
            {
                result.Add(IncidentRealtimeGroups.IncidentScope(incidentId));
            }

            if (!string.IsNullOrWhiteSpace(scope.ClientUserId))
            {
                result.Add(IncidentRealtimeGroups.ClientScope(scope.ClientUserId));
            }
            result.Add(IncidentRealtimeGroups.ClientScopeAll());

            if (!string.IsNullOrWhiteSpace(scope.RegionCode))
            {
                result.Add(IncidentRealtimeGroups.RegionScope(scope.RegionCode));
            }
            result.Add(IncidentRealtimeGroups.RegionScopeAll());

            if (!string.IsNullOrWhiteSpace(scope.ShiftKey))
            {
                result.Add(IncidentRealtimeGroups.ShiftScope(scope.ShiftKey));
            }
            result.Add(IncidentRealtimeGroups.ShiftScopeAll());
        }
        else
        {
            result.Add(IncidentRealtimeGroups.OperatorRole);
        }

        return result;
    }

    private static bool TryReadScope(JsonElement payload, out RealtimeScopeDto scope)
    {
        scope = new RealtimeScopeDto();
        if (!payload.TryGetProperty("scope", out var scopeJson))
        {
            return false;
        }

        var deserialized = scopeJson.Deserialize<RealtimeScopeDto>();
        if (deserialized is null)
        {
            return false;
        }

        scope = deserialized;
        return true;
    }
}
