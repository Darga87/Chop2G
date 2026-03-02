using Chop.Domain.Platform;
using Chop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Chop.Api.Platform;

public sealed class OutboxFailuresHealthCheck : IHealthCheck
{
    private readonly AppDbContext _dbContext;
    private readonly PlatformReliabilityOptions _options;

    public OutboxFailuresHealthCheck(AppDbContext dbContext, IOptions<PlatformReliabilityOptions> options)
    {
        _dbContext = dbContext;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var lookback = DateTime.UtcNow.AddMinutes(-Math.Max(_options.OutboxFailedLookbackMinutes, 1));
        var failedCount = await _dbContext.OutboxMessages
            .Where(x => x.Status == OutboxMessageStatus.Failed)
            .Where(x => x.CreatedAtUtc >= lookback)
            .CountAsync(cancellationToken);

        if (failedCount > _options.OutboxFailedUnhealthyThresholdCount)
        {
            return HealthCheckResult.Unhealthy(
                $"Outbox failed messages in last {_options.OutboxFailedLookbackMinutes}m: {failedCount} (threshold {_options.OutboxFailedUnhealthyThresholdCount}).");
        }

        return HealthCheckResult.Healthy(
            $"Outbox failed messages in last {_options.OutboxFailedLookbackMinutes}m: {failedCount}.");
    }
}
