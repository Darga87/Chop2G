using Chop.Api.Auth;
using Chop.Application.Guards;
using Chop.Application.Alerts;
using Chop.Api.Incidents;
using Chop.Domain.Incidents;
using Chop.Infrastructure.Persistence;
using Chop.Shared.Contracts.Guards;
using Chop.Shared.Contracts.Incidents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Chop.Api.Guards;

[ApiController]
[Route("api/guard/location")]
public sealed class GuardLocationController : ControllerBase
{
    private readonly IGuardLocationService _guardLocationService;
    private readonly IIncidentRealtimePublisher _realtimePublisher;
    private readonly IAlertEventsService _alertEventsService;
    private readonly AppDbContext _dbContext;

    public GuardLocationController(
        IGuardLocationService guardLocationService,
        IIncidentRealtimePublisher realtimePublisher,
        IAlertEventsService alertEventsService,
        AppDbContext dbContext)
    {
        _guardLocationService = guardLocationService;
        _realtimePublisher = realtimePublisher;
        _alertEventsService = alertEventsService;
        _dbContext = dbContext;
    }

    [HttpPost("ping")]
    [Authorize(Roles = "GUARD")]
    [EnableRateLimiting("guard-ping")]
    public async Task<IActionResult> Ping([FromBody] GuardLocationPingDto request, CancellationToken cancellationToken)
    {
        var guardUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(guardUserId))
        {
            return Unauthorized();
        }

        if (request.IncidentId is Guid incidentId)
        {
            var isAssignedGuard = await _dbContext.IncidentAssignments
                .AsNoTracking()
                .AnyAsync(
                    x => x.IncidentId == incidentId
                        && x.GuardUserId == guardUserId
                        && x.Status != IncidentAssignmentStatus.Finished,
                    cancellationToken);

            if (!isAssignedGuard)
            {
                return Forbid();
            }
        }

        var result = await _guardLocationService.PingAsync(guardUserId, request, cancellationToken);
        if (request.IncidentId is Guid incidentIdForAlerts)
        {
            await _alertEventsService.ResolveGuardNoPingAlertAsync(incidentIdForAlerts, cancellationToken);
            await _alertEventsService.ResolveGuardOfflineAlertAsync(incidentIdForAlerts, cancellationToken);
        }
        if (result.ShouldPublishRealtime)
        {
            await _realtimePublisher.PublishGuardLocationUpdatedAsync(
                request.IncidentId,
                guardUserId,
                new IncidentLocationDto
                {
                    Lat = request.Lat,
                    Lon = request.Lon,
                    AccuracyM = request.AccuracyM,
                },
                cancellationToken);
        }

        return NoContent();
    }
}
