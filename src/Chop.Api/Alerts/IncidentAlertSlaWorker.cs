using Chop.Application.Alerts;
using Chop.Domain.Incidents;
using Chop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Chop.Api.Alerts;

public sealed class IncidentAlertSlaWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AlertSlaOptions _options;
    private readonly ILogger<IncidentAlertSlaWorker> _logger;

    public IncidentAlertSlaWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<AlertSlaOptions> options,
        ILogger<IncidentAlertSlaWorker> logger)
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
                await RunIterationAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Incident alert SLA worker iteration failed.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(_options.PollIntervalMs), stoppingToken);
        }
    }

    private async Task RunIterationAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var alertEvents = scope.ServiceProvider.GetRequiredService<IAlertEventsService>();
        var now = DateTime.UtcNow;
        var noAcceptThreshold = now.AddSeconds(-Math.Max(_options.NoAcceptStuckSeconds, 1));
        var guardOfflineThreshold = now.AddSeconds(-Math.Max(_options.GuardOfflineSeconds, 1));
        var stuckInStatusThreshold = now.AddSeconds(-Math.Max(_options.StuckInStatusSeconds, 1));

        var activeIncidents = await dbContext.Incidents
            .AsNoTracking()
            .Where(x => x.Status == IncidentStatus.Dispatched
                        || x.Status == IncidentStatus.Accepted
                        || x.Status == IncidentStatus.EnRoute
                        || x.Status == IncidentStatus.OnScene)
            .Select(x => new { x.Id, x.Status, x.LastUpdatedAtUtc })
            .ToArrayAsync(cancellationToken);

        foreach (var incident in activeIncidents)
        {
            if (incident.Status == IncidentStatus.Dispatched)
            {
                var hasAcceptedGuard = await dbContext.DispatchRecipients
                    .Include(x => x.Dispatch)
                    .Where(x => x.Dispatch!.IncidentId == incident.Id)
                    .Where(x => x.RecipientType == DispatchRecipientType.Guard)
                    .AnyAsync(x => x.Status == DispatchRecipientStatus.Accepted, cancellationToken);

                if (!hasAcceptedGuard)
                {
                    var firstDispatchAt = await dbContext.Dispatches
                        .Where(x => x.IncidentId == incident.Id)
                        .OrderBy(x => x.CreatedAtUtc)
                        .Select(x => (DateTime?)x.CreatedAtUtc)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (firstDispatchAt.HasValue && firstDispatchAt.Value <= noAcceptThreshold)
                    {
                        await alertEvents.EnsureNoAcceptStuckAlertAsync(incident.Id, cancellationToken);
                    }
                }
                else
                {
                    await alertEvents.ResolveNoAcceptStuckAlertAsync(incident.Id, cancellationToken);
                }
            }
            else
            {
                await alertEvents.ResolveNoAcceptStuckAlertAsync(incident.Id, cancellationToken);
            }

            if (incident.Status is IncidentStatus.Accepted or IncidentStatus.EnRoute or IncidentStatus.OnScene)
            {
                var hasAcceptedGuard = await dbContext.DispatchRecipients
                    .Include(x => x.Dispatch)
                    .Where(x => x.Dispatch!.IncidentId == incident.Id)
                    .Where(x => x.RecipientType == DispatchRecipientType.Guard)
                    .AnyAsync(x => x.Status == DispatchRecipientStatus.Accepted, cancellationToken);

                if (hasAcceptedGuard)
                {
                    var latestPingAt = await dbContext.GuardLocations
                        .Where(x => x.IncidentId == incident.Id)
                        .OrderByDescending(x => x.UpdatedAtUtc)
                        .Select(x => (DateTime?)x.UpdatedAtUtc)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (latestPingAt.HasValue && latestPingAt.Value <= guardOfflineThreshold)
                    {
                        await alertEvents.EnsureGuardOfflineAlertAsync(incident.Id, cancellationToken);
                    }
                    else
                    {
                        await alertEvents.ResolveGuardOfflineAlertAsync(incident.Id, cancellationToken);
                    }
                }
                else
                {
                    await alertEvents.ResolveGuardOfflineAlertAsync(incident.Id, cancellationToken);
                }
            }
            else
            {
                await alertEvents.ResolveGuardOfflineAlertAsync(incident.Id, cancellationToken);
            }

            if (incident.Status is IncidentStatus.EnRoute or IncidentStatus.OnScene)
            {
                if (incident.LastUpdatedAtUtc <= stuckInStatusThreshold)
                {
                    await alertEvents.EnsureStuckInStatusAlertAsync(incident.Id, cancellationToken);
                }
                else
                {
                    await alertEvents.ResolveStuckInStatusAlertAsync(incident.Id, cancellationToken);
                }
            }
            else
            {
                await alertEvents.ResolveStuckInStatusAlertAsync(incident.Id, cancellationToken);
            }
        }
    }
}
