using Chop.Api.Auth;
using Chop.Application.Alerts;
using Chop.Shared.Contracts.Alerts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chop.Api.Alerts;

[ApiController]
[Route("api/operator")]
public sealed class OperatorAlertsController : ControllerBase
{
    private readonly IAlertEventsService _alertEventsService;

    public OperatorAlertsController(IAlertEventsService alertEventsService)
    {
        _alertEventsService = alertEventsService;
    }

    [HttpGet("incidents/{incidentId:guid}/alerts")]
    [Authorize(Roles = "OPERATOR,ADMIN,SUPERADMIN")]
    public async Task<ActionResult<IReadOnlyCollection<AlertListItemDto>>> ListIncidentAlerts(
        Guid incidentId,
        [FromQuery] bool includeResolved = false,
        CancellationToken cancellationToken = default)
    {
        var items = await _alertEventsService.ListIncidentAlertsAsync(incidentId, includeResolved, cancellationToken);
        return Ok(items);
    }

    [HttpPost("alerts/{alertId:guid}/ack")]
    [Authorize(Roles = "OPERATOR,ADMIN,SUPERADMIN")]
    public async Task<IActionResult> Ack(Guid alertId, [FromBody] AckAlertRequestDto request, CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        var actorRole = User.GetHighestRole();
        if (string.IsNullOrWhiteSpace(actorUserId) || string.IsNullOrWhiteSpace(actorRole))
        {
            return Unauthorized();
        }

        try
        {
            await _alertEventsService.AckAsync(alertId, actorUserId, actorRole, request.Comment, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (string.Equals(ex.Message, "Alert not found.", StringComparison.Ordinal))
        {
            return NotFound(new { code = "ALERT_NOT_FOUND", message = ex.Message });
        }
    }

    [HttpPost("alerts/{alertId:guid}/resolve")]
    [Authorize(Roles = "OPERATOR,ADMIN,SUPERADMIN")]
    public async Task<IActionResult> Resolve(Guid alertId, [FromBody] ResolveAlertRequestDto request, CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        var actorRole = User.GetHighestRole();
        if (string.IsNullOrWhiteSpace(actorUserId) || string.IsNullOrWhiteSpace(actorRole))
        {
            return Unauthorized();
        }

        try
        {
            await _alertEventsService.ResolveAsync(alertId, actorUserId, actorRole, request.Comment, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (string.Equals(ex.Message, "Alert not found.", StringComparison.Ordinal))
        {
            return NotFound(new { code = "ALERT_NOT_FOUND", message = ex.Message });
        }
    }

    [HttpPost("alerts/{alertId:guid}/assign")]
    [Authorize(Roles = "OPERATOR,ADMIN,SUPERADMIN")]
    public async Task<IActionResult> Assign(Guid alertId, [FromBody] AssignAlertRequestDto request, CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        var actorRole = User.GetHighestRole();
        if (string.IsNullOrWhiteSpace(actorUserId) || string.IsNullOrWhiteSpace(actorRole))
        {
            return Unauthorized();
        }

        var assignee = string.IsNullOrWhiteSpace(request.AssigneeUserId) ? actorUserId : request.AssigneeUserId.Trim();
        try
        {
            await _alertEventsService.AssignAsync(alertId, actorUserId, actorRole, assignee, request.Comment, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (string.Equals(ex.Message, "Alert not found.", StringComparison.Ordinal))
        {
            return NotFound(new { code = "ALERT_NOT_FOUND", message = ex.Message });
        }
    }

    [HttpPost("alerts/{alertId:guid}/override")]
    [Authorize(Roles = "OPERATOR,ADMIN,SUPERADMIN")]
    public async Task<IActionResult> Override(Guid alertId, [FromBody] OverrideAlertRequestDto request, CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        var actorRole = User.GetHighestRole();
        if (string.IsNullOrWhiteSpace(actorUserId) || string.IsNullOrWhiteSpace(actorRole))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Comment))
        {
            return BadRequest(new { code = "OVERRIDE_COMMENT_REQUIRED", message = "Для оверрайда требуется комментарий." });
        }

        try
        {
            await _alertEventsService.OverrideAsync(alertId, actorUserId, actorRole, request.Comment, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (string.Equals(ex.Message, "Alert not found.", StringComparison.Ordinal))
        {
            return NotFound(new { code = "ALERT_NOT_FOUND", message = ex.Message });
        }
    }
}
