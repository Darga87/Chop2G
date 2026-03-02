using System.Text;
using System.Text.Json;
using Chop.Api.Incidents;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Chop.Api.Platform;

public sealed class RealtimeBusConsumer : BackgroundService
{
    private readonly RealtimeBusOptions _options;
    private readonly IHubContext<IncidentsHub> _hubContext;
    private readonly ILogger<RealtimeBusConsumer> _logger;

    private IConnection? _connection;
    private IModel? _channel;

    public RealtimeBusConsumer(
        IOptions<RealtimeBusOptions> options,
        IHubContext<IncidentsHub> hubContext,
        ILogger<RealtimeBusConsumer> logger)
    {
        _options = options.Value;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Realtime bus consumer is disabled.");
            return;
        }

        Initialize();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stopRegistration = stoppingToken.Register(() => completion.TrySetResult());

        var consumer = new AsyncEventingBasicConsumer(_channel!);
        consumer.Received += async (_, ea) =>
        {
            var success = await HandleAsync(ea, stoppingToken);
            if (success)
            {
                _channel!.BasicAck(ea.DeliveryTag, multiple: false);
            }
            else
            {
                _channel!.BasicNack(ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        _channel!.BasicConsume(
            queue: _options.Queue,
            autoAck: false,
            consumer: consumer);

        await completion.Task;
    }

    public override void Dispose()
    {
        try
        {
            _channel?.Close();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Ignoring realtime consumer channel close failure.");
        }

        try
        {
            _connection?.Close();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Ignoring realtime consumer connection close failure.");
        }

        base.Dispose();
    }

    private void Initialize()
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            VirtualHost = _options.VirtualHost,
            UserName = _options.UserName,
            Password = _options.Password,
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
        };

        _connection = factory.CreateConnection("chop-api-realtime-consumer");
        _channel = _connection.CreateModel();
        _channel.ExchangeDeclare(
            exchange: _options.Exchange,
            type: _options.ExchangeType,
            durable: _options.Durable,
            autoDelete: false,
            arguments: null);
        _channel.QueueDeclare(
            queue: _options.Queue,
            durable: _options.Durable,
            exclusive: false,
            autoDelete: false,
            arguments: null);
        _channel.QueueBind(
            queue: _options.Queue,
            exchange: _options.Exchange,
            routingKey: _options.RoutingKeyPattern);
        _channel.BasicQos(prefetchSize: 0, prefetchCount: 50, global: false);
    }

    private async Task<bool> HandleAsync(BasicDeliverEventArgs args, CancellationToken cancellationToken)
    {
        RealtimeBusEnvelope envelope;
        try
        {
            var json = Encoding.UTF8.GetString(args.Body.ToArray());
            envelope = JsonSerializer.Deserialize<RealtimeBusEnvelope>(json) ?? new RealtimeBusEnvelope();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Realtime bus message deserialize failed; dropping message.");
            return true;
        }

        if (string.IsNullOrWhiteSpace(envelope.EventType))
        {
            _logger.LogWarning("Realtime bus message has empty EventType; dropping message.");
            return true;
        }

        try
        {
            var method = RealtimeRouting.ResolveMethod(envelope.EventType);
            var payload = JsonSerializer.Deserialize<JsonElement>(envelope.PayloadJson);
            foreach (var destination in RealtimeRouting.ResolveDestinations(payload))
            {
                await _hubContext.Clients.Group(destination)
                    .SendAsync(method, payload, cancellationToken);
            }

            return true;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Unsupported realtime event type {EventType}; dropping message.", envelope.EventType);
            return true;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid payload json for realtime event {EventType}; dropping message.", envelope.EventType);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Realtime bus message handling failed for {EventType}; requeue.", envelope.EventType);
            return false;
        }
    }
}
