using Chop.Domain.Platform;
using Chop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Chop.Api.Platform;

public sealed class OutboxMessageProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PlatformReliabilityOptions _options;
    private readonly ILogger<OutboxMessageProcessor> _logger;
    private readonly Random _random = new();

    public OutboxMessageProcessor(
        IServiceScopeFactory scopeFactory,
        IOptions<PlatformReliabilityOptions> options,
        ILogger<OutboxMessageProcessor> logger)
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
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Outbox message processor iteration failed.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(_options.OutboxPollIntervalMs), stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IOutboxEventPublisher>();
        var now = DateTime.UtcNow;

        var batch = await dbContext.OutboxMessages
            .Where(x => x.Status == OutboxMessageStatus.Pending)
            .Where(x => !x.NextAttemptAtUtc.HasValue || x.NextAttemptAtUtc <= now)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(_options.OutboxBatchSize)
            .ToArrayAsync(cancellationToken);

        var published = 0;
        var failed = 0;

        foreach (var message in batch)
        {
            try
            {
                await publisher.PublishAsync(message.EventType, message.PayloadJson, cancellationToken);
                message.AttemptCount++;
                message.Status = OutboxMessageStatus.Published;
                message.PublishedAtUtc = DateTime.UtcNow;
                message.NextAttemptAtUtc = null;
                message.LastError = null;
                published++;
            }
            catch (Exception ex)
            {
                message.AttemptCount++;
                if (message.AttemptCount >= _options.OutboxMaxAttempts)
                {
                    message.Status = OutboxMessageStatus.Failed;
                    message.NextAttemptAtUtc = null;
                }
                else
                {
                    message.NextAttemptAtUtc = DateTime.UtcNow.AddSeconds(GetRetryDelaySeconds(message.AttemptCount));
                }

                message.LastError = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
                failed++;

                _logger.LogWarning(ex, "Outbox publish failed. MessageId={MessageId}", message.Id);
            }
        }

        if (batch.Length > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Outbox batch processed Count={Count} Published={Published} Failed={Failed}",
                batch.Length,
                published,
                failed);
        }
    }

    private int GetRetryDelaySeconds(int attempt)
    {
        var baseSeconds = Math.Max(_options.OutboxRetryBaseSeconds, 1);
        var maxSeconds = Math.Max(_options.OutboxRetryMaxSeconds, baseSeconds);
        var delay = Math.Min(maxSeconds, baseSeconds * (int)Math.Pow(2, Math.Min(attempt - 1, 8)));
        var jitter = _random.NextDouble() * 0.2 + 0.9;
        return Math.Max(1, (int)Math.Round(delay * jitter));
    }
}
