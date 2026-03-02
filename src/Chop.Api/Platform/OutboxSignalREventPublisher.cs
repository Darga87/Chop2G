using System.Text.Json;
using Chop.Api.Incidents;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace Chop.Api.Platform;

public sealed class OutboxSignalREventPublisher : IOutboxEventPublisher
{
    private readonly IHubContext<IncidentsHub> _hubContext;
    private readonly IRealtimeBusPublisher _busPublisher;
    private readonly RealtimeBusOptions _busOptions;
    private readonly ILogger<OutboxSignalREventPublisher> _logger;

    public OutboxSignalREventPublisher(
        IHubContext<IncidentsHub> hubContext,
        IRealtimeBusPublisher busPublisher,
        IOptions<RealtimeBusOptions> busOptions,
        ILogger<OutboxSignalREventPublisher> logger)
    {
        _hubContext = hubContext;
        _busPublisher = busPublisher;
        _busOptions = busOptions.Value;
        _logger = logger;
    }

    public async Task PublishAsync(string eventType, string payloadJson, CancellationToken cancellationToken)
    {
        if (_busOptions.Enabled)
        {
            await _busPublisher.PublishAsync(eventType, payloadJson, cancellationToken);
            return;
        }

        await PublishToSignalRAsync(eventType, payloadJson, cancellationToken);
    }

    internal async Task PublishToSignalRAsync(string eventType, string payloadJson, CancellationToken cancellationToken)
    {
        var method = RealtimeRouting.ResolveMethod(eventType);
        JsonElement payload;

        try
        {
            payload = JsonSerializer.Deserialize<JsonElement>(payloadJson);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid outbox payload json. EventType={EventType}", eventType);
            throw;
        }

        foreach (var destination in RealtimeRouting.ResolveDestinations(payload))
        {
            await _hubContext.Clients.Group(destination)
                .SendAsync(method, payload, cancellationToken);
        }
    }
}
