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
        await _hubContext.Clients.Group(IncidentRealtimeGroups.Ops)
            .SendAsync(method, payload, cancellationToken);
    }
}
