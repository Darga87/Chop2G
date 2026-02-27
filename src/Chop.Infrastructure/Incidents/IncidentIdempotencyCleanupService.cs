using Chop.Application.Incidents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Chop.Infrastructure.Incidents;

public sealed class IncidentIdempotencyCleanupService : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(30);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IncidentIdempotencyCleanupService> _logger;

    public IncidentIdempotencyCleanupService(IServiceScopeFactory scopeFactory, ILogger<IncidentIdempotencyCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup expired incident idempotency records.");
            }

            await Task.Delay(SweepInterval, stoppingToken);
        }
    }

    private async Task CleanupExpiredAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IIncidentIdempotencyRepository>();
        var removed = await repository.DeleteExpiredAsync(DateTime.UtcNow, cancellationToken);
        if (removed > 0)
        {
            _logger.LogInformation("Removed {Count} expired incident idempotency records.", removed);
        }
    }
}
