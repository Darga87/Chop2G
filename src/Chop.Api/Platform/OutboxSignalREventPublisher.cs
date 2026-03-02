using System.Text.Json;
using Chop.Api.Incidents;
using Chop.Shared.Contracts.Realtime;
using Microsoft.AspNetCore.SignalR;

namespace Chop.Api.Platform;

public sealed class OutboxSignalREventPublisher : IOutboxEventPublisher
{
    private readonly IHubContext<IncidentsHub> _hubContext;

    public OutboxSignalREventPublisher(IHubContext<IncidentsHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task PublishAsync(string eventType, string payloadJson, CancellationToken cancellationToken)
    {
        var method = eventType switch
        {
            "realtime.incident-created" => "IncidentCreated",
            "realtime.incident-status-changed" => "IncidentStatusChanged",
            "realtime.dispatch-created" => "DispatchCreated",
            "realtime.dispatch-accepted" => "DispatchAccepted",
            "realtime.guard-location-updated" => "GuardLocationUpdated",
            _ => throw new InvalidOperationException($"Unsupported outbox event type '{eventType}'."),
        };

        var payload = JsonSerializer.Deserialize<JsonElement>(payloadJson);
        foreach (var destination in ResolveDestinations(payload))
        {
            await _hubContext.Clients.Group(destination)
                .SendAsync(method, payload, cancellationToken);
        }
    }

    private static IReadOnlyCollection<string> ResolveDestinations(JsonElement payload)
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
            // Backward-compatible fallback for payloads without scope metadata.
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
