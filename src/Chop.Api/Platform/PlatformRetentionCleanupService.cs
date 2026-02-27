using Chop.Domain.Platform;
using Chop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Chop.Api.Platform;

public sealed class PlatformRetentionCleanupService : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PlatformReliabilityOptions _options;
    private readonly ILogger<PlatformRetentionCleanupService> _logger;

    public PlatformRetentionCleanupService(
        IServiceScopeFactory scopeFactory,
        IOptions<PlatformReliabilityOptions> options,
        ILogger<PlatformRetentionCleanupService> logger)
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
                await CleanupAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Platform retention cleanup failed.");
            }

            await Task.Delay(SweepInterval, stoppingToken);
        }
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        var outboxBefore = now.AddDays(-Math.Max(_options.OutboxRetentionDays, 1));
        var auditBefore = now.AddDays(-Math.Max(_options.AuditRetentionDays, 1));

        var oldOutbox = await dbContext.OutboxMessages
            .Where(x => x.CreatedAtUtc < outboxBefore)
            .Where(x => x.Status != OutboxMessageStatus.Pending)
            .ToListAsync(cancellationToken);

        var oldAudit = await dbContext.AuditLogEntries
            .Where(x => x.CreatedAtUtc < auditBefore)
            .ToListAsync(cancellationToken);

        if (oldOutbox.Count > 0)
        {
            dbContext.OutboxMessages.RemoveRange(oldOutbox);
        }

        if (oldAudit.Count > 0)
        {
            dbContext.AuditLogEntries.RemoveRange(oldAudit);
        }

        if (oldOutbox.Count > 0 || oldAudit.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Platform cleanup removed Outbox={OutboxCount} Audit={AuditCount}",
                oldOutbox.Count,
                oldAudit.Count);
        }
    }
}
