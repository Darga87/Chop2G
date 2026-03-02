using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Chop.Api.Platform;

public sealed class RabbitRealtimeBusPublisher : IRealtimeBusPublisher, IDisposable
{
    private readonly RealtimeBusOptions _options;
    private readonly ILogger<RabbitRealtimeBusPublisher> _logger;
    private readonly object _sync = new();

    private IConnection? _connection;
    private IModel? _channel;

    public RabbitRealtimeBusPublisher(
        IOptions<RealtimeBusOptions> options,
        ILogger<RabbitRealtimeBusPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsEnabled => _options.Enabled;

    public Task PublishAsync(string eventType, string payloadJson, CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return Task.CompletedTask;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        var envelope = new RealtimeBusEnvelope
        {
            EventType = eventType,
            PayloadJson = payloadJson,
            PublishedAtUtc = DateTime.UtcNow,
        };

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope));

        lock (_sync)
        {
            EnsureConnected();

            var properties = _channel!.CreateBasicProperties();
            properties.Persistent = _options.Durable;

            _channel.BasicPublish(
                exchange: _options.Exchange,
                routingKey: eventType,
                basicProperties: properties,
                body: body);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        lock (_sync)
        {
            try
            {
                _channel?.Close();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Ignoring realtime bus channel close failure.");
            }

            try
            {
                _connection?.Close();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Ignoring realtime bus connection close failure.");
            }
        }
    }

    private void EnsureConnected()
    {
        if (_channel is { IsOpen: true } && _connection is { IsOpen: true })
        {
            return;
        }

        _channel?.Dispose();
        _connection?.Dispose();
        _channel = null;
        _connection = null;

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

        _connection = factory.CreateConnection("chop-api-outbox-publisher");
        _channel = _connection.CreateModel();
        _channel.ExchangeDeclare(
            exchange: _options.Exchange,
            type: _options.ExchangeType,
            durable: _options.Durable,
            autoDelete: false,
            arguments: null);
    }
}
