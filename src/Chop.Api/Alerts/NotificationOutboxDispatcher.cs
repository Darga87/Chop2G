using System.Text.Json;
using Chop.Api.Incidents;
using Chop.Domain.Alerts;
using Chop.Infrastructure.Alerts;
using Chop.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Chop.Api.Alerts;

public sealed class NotificationOutboxDispatcher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly NotificationOutboxOptions _options;
    private readonly ILogger<NotificationOutboxDispatcher> _logger;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public NotificationOutboxDispatcher(
        IServiceScopeFactory scopeFactory,
        IOptions<NotificationOutboxOptions> options,
        ILogger<NotificationOutboxDispatcher> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Notification outbox dispatcher iteration failed.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(_options.PollIntervalMs), stoppingToken);
        }
    }

    private async Task DispatchBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<IncidentsHub>>();
        var now = DateTime.UtcNow;

        var batch = await dbContext.NotificationOutbox
            .Where(x => x.Status == NotificationOutboxStatus.Pending)
            .Where(x => !x.NextAttemptAtUtc.HasValue || x.NextAttemptAtUtc <= now)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(_options.BatchSize)
            .ToArrayAsync(cancellationToken);

        foreach (var item in batch)
        {
            try
            {
                await DeliverAsync(item, hubContext, cancellationToken);
                item.Status = NotificationOutboxStatus.Sent;
                item.AttemptCount++;
                item.NextAttemptAtUtc = null;
                dbContext.NotificationDeliveries.Add(new NotificationDelivery
                {
                    Id = Guid.NewGuid(),
                    OutboxId = item.Id,
                    Status = NotificationDeliveryStatus.Sent,
                    ProviderResponse = "Delivered",
                    CreatedAtUtc = DateTime.UtcNow,
                });
            }
            catch (Exception ex)
            {
                item.AttemptCount++;
                item.Status = item.AttemptCount >= _options.MaxAttempts
                    ? NotificationOutboxStatus.Failed
                    : NotificationOutboxStatus.Pending;
                item.NextAttemptAtUtc = item.Status == NotificationOutboxStatus.Pending
                    ? DateTime.UtcNow.AddSeconds(GetRetrySeconds(item.AttemptCount))
                    : null;

                dbContext.NotificationDeliveries.Add(new NotificationDelivery
                {
                    Id = Guid.NewGuid(),
                    OutboxId = item.Id,
                    Status = NotificationDeliveryStatus.Failed,
                    ProviderResponse = ex.Message.Length > 512 ? ex.Message[..512] : ex.Message,
                    CreatedAtUtc = DateTime.UtcNow,
                });

                _logger.LogWarning(ex, "Notification delivery failed. OutboxId={OutboxId}", item.Id);
            }
        }

        if (batch.Length > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task DeliverAsync(
        NotificationOutbox item,
        IHubContext<IncidentsHub> hubContext,
        CancellationToken cancellationToken)
    {
        if (item.Channel != NotificationChannel.SignalR)
        {
            throw new InvalidOperationException($"Unsupported notification channel '{item.Channel}'.");
        }

        var envelope = JsonSerializer.Deserialize<SignalRNotificationEnvelope>(item.PayloadJson, SerializerOptions)
            ?? throw new InvalidOperationException("Failed to deserialize notification envelope.");
        var payload = JsonSerializer.Deserialize<JsonElement>(envelope.PayloadJson, SerializerOptions);

        await hubContext.Clients.Group(item.Destination)
            .SendAsync(envelope.Method, payload, cancellationToken);
    }

    private static int GetRetrySeconds(int attempt)
    {
        var seconds = (int)Math.Pow(2, Math.Min(attempt, 6));
        return Math.Min(seconds, 60);
    }
}
