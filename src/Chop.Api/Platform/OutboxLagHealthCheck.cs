using Chop.Domain.Platform;
using Chop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Chop.Api.Platform;

public sealed class OutboxLagHealthCheck : IHealthCheck
{
    private readonly AppDbContext _dbContext;
    private readonly PlatformReliabilityOptions _options;

    public OutboxLagHealthCheck(AppDbContext dbContext, IOptions<PlatformReliabilityOptions> options)
    {
        _dbContext = dbContext;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var oldestPending = await _dbContext.OutboxMessages
            .Where(x => x.Status == OutboxMessageStatus.Pending)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => (DateTime?)x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (!oldestPending.HasValue)
        {
            return HealthCheckResult.Healthy("No pending outbox messages.");
        }

        var lag = DateTime.UtcNow - oldestPending.Value;
        if (lag.TotalSeconds > _options.OutboxLagUnhealthyThresholdSeconds)
        {
            return HealthCheckResult.Unhealthy(
                $"Outbox lag is {lag.TotalSeconds:F0}s, threshold {_options.OutboxLagUnhealthyThresholdSeconds}s.");
        }

        return HealthCheckResult.Healthy($"Outbox lag is {lag.TotalSeconds:F0}s.");
    }
}
