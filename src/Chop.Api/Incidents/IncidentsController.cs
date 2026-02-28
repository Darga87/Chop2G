using Chop.Api.Auth;
using Chop.Application.Incidents;
using Chop.Application.Platform;
using Chop.Shared.Contracts.Incidents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chop.Api.Incidents;

[ApiController]
[Route("api")]
public sealed class IncidentsController : ControllerBase
{
    private readonly IIncidentService _incidentService;
    private readonly IIncidentRealtimePublisher _realtimePublisher;
    private readonly IAuditLogService _auditLogService;

    public IncidentsController(
        IIncidentService incidentService,
        IIncidentRealtimePublisher realtimePublisher,
        IAuditLogService auditLogService)
    {
        _incidentService = incidentService;
        _realtimePublisher = realtimePublisher;
        _auditLogService = auditLogService;
    }

    [HttpPost("incidents")]
    [Authorize(Roles = "CLIENT")]
    public async Task<ActionResult<CreateIncidentResponseDto>> CreateIncident(
        [FromBody] CreateIncidentDto request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        try
        {
            var result = await _incidentService.CreateAsync(request, userId, idempotencyKey, cancellationToken);
            await _realtimePublisher.PublishIncidentCreatedAsync(result.Incident, cancellationToken);
            await _auditLogService.WriteAsync(
                "incident.create",
                "incident",
                result.IncidentId,
                userId,
                "CLIENT",
                """{"source":"api/incidents"}""",
                cancellationToken);
            return CreatedAtAction(nameof(GetIncidentById), new { id = result.IncidentId }, result);
        }
        catch (IdempotencyKeyConflictException)
        {
            return Conflict(new
            {
                code = "IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_PAYLOAD",
                message = "Idempotency-Key already used with another request payload.",
            });
        }
    }

    [HttpGet("operator/incidents")]
    [Authorize(Roles = "OPERATOR,ADMIN,SUPERADMIN")]
    public Task<ActionResult<Chop.Shared.Contracts.Common.PagedResult<IncidentListItemDto>>> GetIncidents(
        [FromQuery] string? status,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        GetIncidentsInternal(status, from, to, page, pageSize, cancellationToken);

    [HttpGet("operator/incidents/{id:guid}")]
    [Authorize(Roles = "OPERATOR,ADMIN,SUPERADMIN")]
    public async Task<ActionResult<IncidentDetailsDto>> GetIncidentById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _incidentService.GetDetailsAsync(id, cancellationToken);
        if (item is not null)
        {
            item = ApplyIncidentDetailsPiiPolicy(item, User.GetHighestRole());
        }

        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("operator/incidents/{id:guid}/nearest")]
    [Authorize(Roles = "OPERATOR,ADMIN,SUPERADMIN")]
    public async Task<ActionResult<NearestForcesDto>> GetNearestForces(
        Guid id,
        [FromQuery] int limitPosts = 5,
        [FromQuery] int limitUnits = 5,
        CancellationToken cancellationToken = default)
    {
        var item = await _incidentService.GetNearestForcesAsync(id, limitPosts, limitUnits, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("operator/incidents/{id:guid}/status")]
    [Authorize(Roles = "OPERATOR,ADMIN,SUPERADMIN")]
    public async Task<ActionResult<IncidentDto>> ChangeStatusByOperator(
        Guid id,
        [FromBody] ChangeIncidentStatusDto request,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        var actorRole = User.GetHighestRole();
        if (string.IsNullOrWhiteSpace(actorUserId) || string.IsNullOrWhiteSpace(actorRole))
        {
            return Unauthorized();
        }

        try
        {
            var existing = await _incidentService.GetDetailsAsync(id, cancellationToken);
            var result = await _incidentService.ChangeStatusByOperatorAsync(id, actorUserId, actorRole, request, cancellationToken);
            if (result is null)
            {
                return NotFound();
            }

            if (existing is not null)
            {
                await _realtimePublisher.PublishIncidentStatusChangedAsync(
                    result,
                    existing.Status,
                    result.Status,
                    actorUserId,
                    actorRole,
                    request.Comment,
                    cancellationToken);
            }
            await _auditLogService.WriteAsync(
                "incident.status.change.operator",
                "incident",
                id,
                actorUserId,
                actorRole,
                $$"""{"toStatus":"{{result.Status}}","hasComment":{{(!string.IsNullOrWhiteSpace(request.Comment)).ToString().ToLowerInvariant()}}}""",
                cancellationToken);

            return Ok(result);
        }
        catch (IncidentStatusTransitionException ex)
        {
            return StatusCode(ex.HttpStatusCode, new { code = ex.Code, message = ex.Message });
        }
    }

    [HttpPost("operator/incidents/{id:guid}/dispatch")]
    [Authorize(Roles = "OPERATOR,ADMIN,SUPERADMIN")]
    public async Task<ActionResult<DispatchDto>> CreateDispatch(
        Guid id,
        [FromBody] CreateDispatchDto request,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        var actorRole = User.GetHighestRole();
        if (string.IsNullOrWhiteSpace(actorUserId) || string.IsNullOrWhiteSpace(actorRole))
        {
            return Unauthorized();
        }

        try
        {
            var existing = await _incidentService.GetDetailsAsync(id, cancellationToken);
            var dispatch = await _incidentService.CreateDispatchAsync(id, actorUserId, actorRole, request, cancellationToken);
            if (dispatch is null)
            {
                return NotFound();
            }

            if (existing is not null)
            {
                var updated = await _incidentService.GetDetailsAsync(id, cancellationToken);
                if (updated is not null)
                {
                    if (!string.Equals(existing.Status, updated.Status, StringComparison.OrdinalIgnoreCase))
                    {
                        await _realtimePublisher.PublishIncidentStatusChangedAsync(
                            ToIncidentDto(updated),
                            existing.Status,
                            updated.Status,
                            actorUserId,
                            actorRole,
                            request.Comment,
                            cancellationToken);
                    }
                }
            }

            await _realtimePublisher.PublishDispatchCreatedAsync(id, cancellationToken);
            await _auditLogService.WriteAsync(
                "incident.dispatch.create",
                "incident",
                id,
                actorUserId,
                actorRole,
                $$"""{"method":"{{request.Method}}","recipientsCount":{{request.Recipients.Count}}}""",
                cancellationToken);
            return Ok(dispatch);
        }
        catch (IncidentStatusTransitionException ex)
        {
            return StatusCode(ex.HttpStatusCode, new { code = ex.Code, message = ex.Message });
        }
    }

    [HttpPost("guard/incidents/{id:guid}/accept")]
    [Authorize(Roles = "GUARD")]
    public async Task<ActionResult<IncidentDto>> GuardAccept(
        Guid id,
        [FromBody] GuardAcceptIncidentDto request,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(actorUserId))
        {
            return Unauthorized();
        }

        try
        {
            var existing = await _incidentService.GetDetailsAsync(id, cancellationToken);
            var result = await _incidentService.GuardAcceptAsync(id, actorUserId, request, cancellationToken);
            if (result is null)
            {
                return NotFound();
            }

            if (existing is not null)
            {
                await _realtimePublisher.PublishIncidentStatusChangedAsync(
                    result,
                    existing.Status,
                    result.Status,
                    actorUserId,
                    "GUARD",
                    request.Comment,
                    cancellationToken);
            }

            await _realtimePublisher.PublishDispatchAcceptedAsync(id, actorUserId, request.Comment, cancellationToken);
            await _auditLogService.WriteAsync(
                "incident.guard.accept",
                "incident",
                id,
                actorUserId,
                "GUARD",
                """{"transition":"DISPATCHED->ACCEPTED"}""",
                cancellationToken);
            return Ok(result);
        }
        catch (IncidentStatusTransitionException ex)
        {
            return StatusCode(ex.HttpStatusCode, new { code = ex.Code, message = ex.Message });
        }
    }

    [HttpPost("guard/incidents/{id:guid}/progress")]
    [Authorize(Roles = "GUARD")]
    public async Task<ActionResult<IncidentDto>> GuardProgress(
        Guid id,
        [FromBody] GuardProgressIncidentDto request,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(actorUserId))
        {
            return Unauthorized();
        }

        try
        {
            var existing = await _incidentService.GetDetailsAsync(id, cancellationToken);
            var result = await _incidentService.GuardProgressAsync(id, actorUserId, request, cancellationToken);
            if (result is null)
            {
                return NotFound();
            }

            if (existing is not null)
            {
                await _realtimePublisher.PublishIncidentStatusChangedAsync(
                    result,
                    existing.Status,
                    result.Status,
                    actorUserId,
                    "GUARD",
                    request.Comment,
                    cancellationToken);
            }
            await _auditLogService.WriteAsync(
                "incident.guard.progress",
                "incident",
                id,
                actorUserId,
                "GUARD",
                $$"""{"toStatus":"{{result.Status}}","hasComment":{{(!string.IsNullOrWhiteSpace(request.Comment)).ToString().ToLowerInvariant()}}}""",
                cancellationToken);

            return Ok(result);
        }
        catch (IncidentStatusTransitionException ex)
        {
            return StatusCode(ex.HttpStatusCode, new { code = ex.Code, message = ex.Message });
        }
    }

    private async Task<ActionResult<Chop.Shared.Contracts.Common.PagedResult<IncidentListItemDto>>> GetIncidentsInternal(
        string? status,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var result = await _incidentService.ListAsync(status, from, to, page, pageSize, cancellationToken);
        return Ok(result);
    }

    private static IncidentDto ToIncidentDto(IncidentDetailsDto details) =>
        new()
        {
            Id = details.Id,
            Status = details.Status,
            CreatedAt = details.CreatedAt,
            Location = details.Location,
            AddressSnapshot = details.AddressSnapshot,
            ClientSummary = details.Client.FullName,
            LastUpdatedAt = details.LastUpdatedAt,
        };

    private static IncidentDetailsDto ApplyIncidentDetailsPiiPolicy(IncidentDetailsDto details, string? actorRole)
    {
        if (!string.Equals(actorRole, "OPERATOR", StringComparison.OrdinalIgnoreCase))
        {
            return details;
        }

        details.Client = new ClientSummaryDto
        {
            FullName = details.Client.FullName,
            Phones = details.Client.Phones
                .Select(x => new ClientPhoneDto
                {
                    Phone = MaskPhone(x.Phone),
                    Type = x.Type,
                    IsPrimary = x.IsPrimary,
                })
                .ToArray(),
        };

        return details;
    }

    private static string MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return string.Empty;
        }

        var digits = phone.Count(char.IsDigit);
        if (digits <= 2)
        {
            return new string('*', phone.Length);
        }

        var digitsToMask = digits - 2;
        var maskedDigits = 0;
        var chars = phone.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (char.IsDigit(chars[i]) && maskedDigits < digitsToMask)
            {
                chars[i] = '*';
                maskedDigits++;
            }
        }

        return new string(chars);
    }
}
