using Chop.Domain.Incidents;
using Chop.Domain.Geo;
using Chop.Application.Alerts;
using Chop.Shared.Contracts.Common;
using Chop.Shared.Contracts.Incidents;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Chop.Application.Incidents;

public interface IIncidentService
{
    Task<CreateIncidentResponseDto> CreateAsync(
        CreateIncidentDto request,
        string clientUserId,
        string? idempotencyKey,
        CancellationToken cancellationToken);

    Task<PagedResult<IncidentListItemDto>> ListAsync(
        string? status,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<IncidentDetailsDto?> GetDetailsAsync(Guid id, CancellationToken cancellationToken);

    Task<NearestForcesDto?> GetNearestForcesAsync(Guid incidentId, int limitPosts, int limitUnits, CancellationToken cancellationToken);

    Task<DispatchDto?> CreateDispatchAsync(
        Guid incidentId,
        string actorUserId,
        string actorRole,
        CreateDispatchDto request,
        CancellationToken cancellationToken);

    Task<IncidentDto?> ChangeStatusByOperatorAsync(
        Guid incidentId,
        string actorUserId,
        string actorRole,
        ChangeIncidentStatusDto request,
        CancellationToken cancellationToken);

    Task<IncidentDto?> GuardAcceptAsync(
        Guid incidentId,
        string actorUserId,
        GuardAcceptIncidentDto request,
        CancellationToken cancellationToken);

    Task<IncidentDto?> GuardProgressAsync(
        Guid incidentId,
        string actorUserId,
        GuardProgressIncidentDto request,
        CancellationToken cancellationToken);
}

    public sealed class IncidentService : IIncidentService
{
    private const int DedupWindowSeconds = 60;
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);
    private readonly IIncidentRepository _repository;
    private readonly IIncidentIdempotencyRepository _idempotencyRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAlertEventsService _alertEventsService;

    public IncidentService(
        IIncidentRepository repository,
        IIncidentIdempotencyRepository idempotencyRepository,
        IUnitOfWork unitOfWork,
        IAlertEventsService alertEventsService)
    {
        _repository = repository;
        _idempotencyRepository = idempotencyRepository;
        _unitOfWork = unitOfWork;
        _alertEventsService = alertEventsService;
    }

    public async Task<CreateIncidentResponseDto> CreateAsync(
        CreateIncidentDto request,
        string clientUserId,
        string? idempotencyKey,
        CancellationToken cancellationToken)
        => await _unitOfWork.ExecuteSerializableAsync(
            ct => CreateCoreAsync(request, clientUserId, idempotencyKey, ct),
            cancellationToken);

    private async Task<CreateIncidentResponseDto> CreateCoreAsync(
        CreateIncidentDto request,
        string clientUserId,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var normalizedKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        var requestHash = normalizedKey is null ? null : ComputeRequestHash(request);

        if (normalizedKey is not null)
        {
            var existingIdempotent = await _idempotencyRepository.FindAsync(clientUserId, normalizedKey, cancellationToken);
            if (existingIdempotent is not null)
            {
                EnsureSamePayload(existingIdempotent, requestHash!);
                var existingIncident = await _repository.GetByIdAsync(existingIdempotent.IncidentId, cancellationToken);
                if (existingIncident is not null)
                {
                    return new CreateIncidentResponseDto
                    {
                        IncidentId = existingIncident.Id,
                        Incident = existingIncident.ToIncidentDto(),
                    };
                }
            }
        }

        var now = DateTime.UtcNow;
        var dedupFrom = now.AddSeconds(-DedupWindowSeconds);
        var recentActive = await _repository.FindRecentActiveAsync(clientUserId, dedupFrom, cancellationToken);
        if (recentActive is not null)
        {
            if (normalizedKey is not null)
            {
                await _idempotencyRepository.AddAsync(
                    BuildIdempotencyRecord(clientUserId, normalizedKey, requestHash!, recentActive.Id, now),
                    cancellationToken);

                try
                {
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                }
                catch (IdempotencyPersistenceRaceException)
                {
                    return await ResolveFromExistingIdempotencyAsync(clientUserId, normalizedKey, requestHash!, cancellationToken);
                }
            }

            return new CreateIncidentResponseDto
            {
                IncidentId = recentActive.Id,
                Incident = recentActive.ToIncidentDto(),
            };
        }

        var incident = new Incident
        {
            Id = Guid.NewGuid(),
            ClientUserId = clientUserId,
            Status = IncidentStatus.Acked,
            Latitude = request.ClientLocation?.Lat,
            Longitude = request.ClientLocation?.Lon,
            GeoPoint = GeoPointHelper.Create(request.ClientLocation?.Lat, request.ClientLocation?.Lon),
            AccuracyM = request.ClientLocation?.AccuracyM,
            DeviceTimeUtc = request.DeviceTimeUtc,
            AddressText = request.AddressText,
            CreatedAtUtc = now,
            LastUpdatedAtUtc = now,
            StatusHistory =
            [
                new IncidentStatusHistory
                {
                    Id = Guid.NewGuid(),
                    FromStatus = null,
                    ToStatus = IncidentStatus.New,
                    ActorUserId = clientUserId,
                    ActorRole = "CLIENT",
                    CreatedAtUtc = now,
                },
                new IncidentStatusHistory
                {
                    Id = Guid.NewGuid(),
                    FromStatus = IncidentStatus.New,
                    ToStatus = IncidentStatus.Acked,
                    ActorUserId = "system",
                    ActorRole = "SYSTEM",
                    CreatedAtUtc = now,
                },
            ],
        };

        await _repository.AddAsync(incident, cancellationToken);
        if (normalizedKey is not null)
        {
            await _idempotencyRepository.AddAsync(
                BuildIdempotencyRecord(clientUserId, normalizedKey, requestHash!, incident.Id, now),
                cancellationToken);
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (IdempotencyPersistenceRaceException) when (normalizedKey is not null)
        {
            return await ResolveFromExistingIdempotencyAsync(clientUserId, normalizedKey!, requestHash!, cancellationToken);
        }

        // Create initial operator alerts (geo completeness).
        await _alertEventsService.EnsureGeoAlertsForIncidentAsync(incident.Id, cancellationToken);

        return new CreateIncidentResponseDto
        {
            IncidentId = incident.Id,
            Incident = incident.ToIncidentDto(),
        };
    }

    public async Task<PagedResult<IncidentListItemDto>> ListAsync(
        string? status,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var parsedStatus = ParseStatus(status);
        var normalizedPage = page < 1 ? 1 : page;
        var normalizedSize = pageSize is < 1 or > 200 ? 20 : pageSize;
        var (items, totalCount) = await _repository.ListAsync(parsedStatus, fromUtc, toUtc, normalizedPage, normalizedSize, cancellationToken);

        return new PagedResult<IncidentListItemDto>
        {
            Items = items.Select(x => x.ToListItemDto()).ToArray(),
            Page = normalizedPage,
            PageSize = normalizedSize,
            TotalCount = totalCount,
        };
    }

    public async Task<IncidentDetailsDto?> GetDetailsAsync(Guid id, CancellationToken cancellationToken)
    {
        var incident = await _repository.GetByIdAsync(id, cancellationToken);
        if (incident is null)
        {
            return null;
        }

        return new IncidentDetailsDto
        {
            Id = incident.Id,
            Status = incident.Status.ToApiStatus(),
            CreatedAt = incident.CreatedAtUtc,
            Location = ToLocation(incident.GeoPoint, incident.AccuracyM),
            AddressSnapshot = incident.AddressText,
            Client = new ClientSummaryDto
            {
                FullName = incident.ClientProfile?.FullName ?? "Unknown client",
                Phones = incident.ClientProfile?.Phones
                    .OrderByDescending(x => x.IsPrimary)
                    .ThenBy(x => x.Type)
                    .Select(x => new ClientPhoneDto
                    {
                        Phone = x.Phone,
                        Type = x.Type,
                        IsPrimary = x.IsPrimary,
                    })
                    .ToArray() ?? [],
            },
            Addresses = incident.ClientProfile?.Addresses
                .OrderByDescending(x => x.IsPrimary)
                .ThenBy(x => x.Label)
                .Select(x => new ClientAddressDto
                {
                    Label = x.Label,
                    Address = x.AddressText,
                    Location = ToLocation(x.GeoPoint, null),
                    IsPrimary = x.IsPrimary,
                })
                .ToArray() ?? [],
            LastUpdatedAt = incident.LastUpdatedAtUtc,
            History = incident.StatusHistory
                .OrderBy(x => x.CreatedAtUtc)
                .Select(x => new IncidentHistoryItemDto
                {
                    FromStatus = x.FromStatus?.ToApiStatus(),
                    ToStatus = x.ToStatus.ToApiStatus(),
                    ActorUserId = x.ActorUserId,
                    ActorRole = x.ActorRole,
                    Comment = x.Comment,
                    CreatedAt = x.CreatedAtUtc,
                })
                .ToArray(),
            Dispatches = incident.Dispatches
                .OrderByDescending(x => x.CreatedAtUtc)
                .Select(x => x.ToDispatchDto())
                .ToArray(),
        };
    }

    public async Task<NearestForcesDto?> GetNearestForcesAsync(Guid incidentId, int limitPosts, int limitUnits, CancellationToken cancellationToken)
    {
        var incident = await _repository.GetByIdAsync(incidentId, cancellationToken);
        if (incident is null)
        {
            return null;
        }

        var normalizedPosts = limitPosts is < 1 or > 100 ? 5 : limitPosts;
        var normalizedUnits = limitUnits is < 1 or > 100 ? 5 : limitUnits;
        var nearestPosts = await _repository.ListNearestPostsAsync(incidentId, normalizedPosts, cancellationToken);
        var nearestUnits = await _repository.ListNearestPatrolUnitsAsync(incidentId, normalizedUnits, cancellationToken);
        var now = DateTime.UtcNow;

        return new NearestForcesDto
        {
            Posts = nearestPosts
                .Select(x => new NearestPostDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    DistanceMeters = Math.Round(x.DistanceMeters, 2),
                    Phone = null,
                    RadioChannel = null,
                    Responsibles = [],
                })
                .ToArray(),
            PatrolUnits = nearestUnits
                .Select(x => new NearestPatrolUnitDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    DistanceMeters = Math.Round(x.DistanceMeters, 2),
                    Phone = null,
                    RadioChannel = null,
                    LastLocationAgeSeconds = x.LastLocationAtUtc.HasValue
                        ? Math.Max(0, (int)(now - x.LastLocationAtUtc.Value).TotalSeconds)
                        : null,
                })
                .ToArray(),
        };
    }

    public async Task<DispatchDto?> CreateDispatchAsync(
        Guid incidentId,
        string actorUserId,
        string actorRole,
        CreateDispatchDto request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(actorRole, "OPERATOR", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(actorRole, "ADMIN", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(actorRole, "SUPERADMIN", StringComparison.OrdinalIgnoreCase))
        {
            throw IncidentStatusTransitionException.BadRequest(
                "ROLE_NOT_ALLOWED",
                "Role is not allowed to create dispatch.");
        }

        var incident = await _repository.GetByIdAsync(incidentId, cancellationToken);
        if (incident is null)
        {
            return null;
        }

        if (incident.Status is not (IncidentStatus.New or IncidentStatus.Acked or IncidentStatus.Dispatched))
        {
            throw IncidentStatusTransitionException.Conflict(
                "INVALID_DISPATCH_TRANSITION",
                "Dispatch can only be created for NEW, ACKED or DISPATCHED incident.");
        }

        if (request.Recipients is null || request.Recipients.Count == 0)
        {
            throw IncidentStatusTransitionException.BadRequest(
                "DISPATCH_RECIPIENTS_REQUIRED",
                "Dispatch requires at least one recipient.");
        }

        var method = ParseDispatchMethodOrThrow(request.Method);
        var recipients = request.Recipients.Select(ToDispatchRecipient).ToArray();
        var now = DateTime.UtcNow;

        var dispatch = new Dispatch
        {
            Id = Guid.NewGuid(),
            IncidentId = incident.Id,
            CreatedByUserId = actorUserId,
            Method = method,
            Comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim(),
            CreatedAtUtc = now,
            Recipients = recipients,
        };

        await _repository.AddDispatchAsync(dispatch, cancellationToken);

        foreach (var recipient in recipients)
        {
            if (recipient.RecipientType == DispatchRecipientType.Guard)
            {
                await _repository.AddAssignmentAsync(new IncidentAssignment
                {
                    Id = Guid.NewGuid(),
                    IncidentId = incident.Id,
                    GuardUserId = recipient.RecipientId,
                    Status = IncidentAssignmentStatus.Assigned,
                    CreatedAtUtc = now,
                }, cancellationToken);
            }
            else if (recipient.RecipientType == DispatchRecipientType.PatrolUnit)
            {
                await _repository.AddAssignmentAsync(new IncidentAssignment
                {
                    Id = Guid.NewGuid(),
                    IncidentId = incident.Id,
                    PatrolUnitId = recipient.RecipientId,
                    Status = IncidentAssignmentStatus.Assigned,
                    CreatedAtUtc = now,
                }, cancellationToken);
            }
        }

        if (incident.Status != IncidentStatus.Dispatched)
        {
            var history = ApplyTransition(incident, IncidentStatus.Dispatched, actorUserId, actorRole, request.Comment);
            await _repository.AddStatusHistoryAsync(history, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // "Always 2 groups" requirement: after first dispatch, surface missing second group as a server-side alert.
        await _alertEventsService.EnsureSecondGroupAlertAsync(incident.Id, cancellationToken);
        await _alertEventsService.EnsureNoAcceptAlertAsync(incident.Id, cancellationToken);

        return dispatch.ToDispatchDto();
    }

    public async Task<IncidentDto?> ChangeStatusByOperatorAsync(
        Guid incidentId,
        string actorUserId,
        string actorRole,
        ChangeIncidentStatusDto request,
        CancellationToken cancellationToken)
    {
        var incident = await _repository.GetByIdAsync(incidentId, cancellationToken);
        if (incident is null)
        {
            return null;
        }

        var target = ParseStatusOrThrow(request.ToStatus, "INVALID_TO_STATUS");
        IncidentStateMachine.ValidateOperatorChange(incident.Status, target, actorRole, request.Comment);

        var history = ApplyTransition(incident, target, actorUserId, actorRole, request.Comment);
        await _repository.AddStatusHistoryAsync(history, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return incident.ToIncidentDto();
    }

    public async Task<IncidentDto?> GuardAcceptAsync(
        Guid incidentId,
        string actorUserId,
        GuardAcceptIncidentDto request,
        CancellationToken cancellationToken)
    {
        var incident = await _repository.GetByIdAsync(incidentId, cancellationToken);
        if (incident is null)
        {
            return null;
        }

        IncidentStateMachine.ValidateGuardAccept(incident.Status);

        var guardRecipient = await _repository.FindGuardRecipientAsync(incidentId, actorUserId, cancellationToken);
        if (guardRecipient is not null && guardRecipient.Status != DispatchRecipientStatus.Accepted)
        {
            guardRecipient.Status = DispatchRecipientStatus.Accepted;
            guardRecipient.AcceptedBy = actorUserId;
            guardRecipient.AcceptedAtUtc = DateTime.UtcNow;
            guardRecipient.AcceptedVia = DispatchAcceptanceVia.App;
        }

        var guardAssignment = await _repository.FindLatestGuardAssignmentAsync(incidentId, actorUserId, cancellationToken);
        if (guardAssignment is not null && guardAssignment.Status != IncidentAssignmentStatus.Accepted)
        {
            guardAssignment.Status = IncidentAssignmentStatus.Accepted;
        }
        else if (guardAssignment is null)
        {
            await _repository.AddAssignmentAsync(new IncidentAssignment
            {
                Id = Guid.NewGuid(),
                IncidentId = incidentId,
                GuardUserId = actorUserId,
                Status = IncidentAssignmentStatus.Accepted,
                CreatedAtUtc = DateTime.UtcNow,
            }, cancellationToken);
        }

        var history = ApplyTransition(incident, IncidentStatus.Accepted, actorUserId, "GUARD", request.Comment);
        await _repository.AddStatusHistoryAsync(history, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Guard accepted: resolve "no accept" alert and create "no ping yet" alert until first geo ping arrives.
        await _alertEventsService.ResolveNoAcceptAlertAsync(incidentId, cancellationToken);
        await _alertEventsService.EnsureGuardNoPingAlertAsync(incidentId, cancellationToken);

        return incident.ToIncidentDto();
    }

    public async Task<IncidentDto?> GuardProgressAsync(
        Guid incidentId,
        string actorUserId,
        GuardProgressIncidentDto request,
        CancellationToken cancellationToken)
    {
        var incident = await _repository.GetByIdAsync(incidentId, cancellationToken);
        if (incident is null)
        {
            return null;
        }

        var target = ParseStatusOrThrow(request.ToStatus, "INVALID_TO_STATUS");
        IncidentStateMachine.ValidateGuardProgress(incident.Status, target, request.Comment);
        var history = ApplyTransition(incident, target, actorUserId, "GUARD", request.Comment);
        await _repository.AddStatusHistoryAsync(history, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return incident.ToIncidentDto();
    }

    private static IncidentStatus? ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        return status.Trim().ToUpperInvariant() switch
        {
            "NEW" => IncidentStatus.New,
            "ACKED" => IncidentStatus.Acked,
            "DISPATCHED" => IncidentStatus.Dispatched,
            "ACCEPTED" => IncidentStatus.Accepted,
            "EN_ROUTE" => IncidentStatus.EnRoute,
            "ON_SCENE" => IncidentStatus.OnScene,
            "RESOLVED" => IncidentStatus.Resolved,
            "CANCELED" => IncidentStatus.Canceled,
            "FALSE_ALARM" => IncidentStatus.FalseAlarm,
            "FAILED" => IncidentStatus.Failed,
            _ => null,
        };
    }

    private static IncidentStatus ParseStatusOrThrow(string? status, string errorCode)
    {
        var parsed = ParseStatus(status);
        if (!parsed.HasValue)
        {
            throw IncidentStatusTransitionException.BadRequest(errorCode, "Unsupported status.");
        }

        return parsed.Value;
    }

    private static DispatchMethod ParseDispatchMethodOrThrow(string? method)
    {
        return method?.Trim().ToUpperInvariant() switch
        {
            "RADIO" => DispatchMethod.Radio,
            "PHONE" => DispatchMethod.Phone,
            "APP" => DispatchMethod.App,
            "MIXED" => DispatchMethod.Mixed,
            _ => throw IncidentStatusTransitionException.BadRequest(
                "INVALID_DISPATCH_METHOD",
                "Unsupported dispatch method."),
        };
    }

    private static DispatchRecipient ToDispatchRecipient(DispatchRecipientInputDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Id))
        {
            throw IncidentStatusTransitionException.BadRequest(
                "INVALID_DISPATCH_RECIPIENT",
                "Dispatch recipient id is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Type))
        {
            throw IncidentStatusTransitionException.BadRequest(
                "INVALID_DISPATCH_RECIPIENT_TYPE",
                "Dispatch recipient type is required.");
        }

        var type = dto.Type.Trim().ToUpperInvariant() switch
        {
            "POST" => DispatchRecipientType.Post,
            "PATROL_UNIT" => DispatchRecipientType.PatrolUnit,
            "GUARD" => DispatchRecipientType.Guard,
            _ => throw IncidentStatusTransitionException.BadRequest(
                "INVALID_DISPATCH_RECIPIENT_TYPE",
                "Unsupported dispatch recipient type."),
        };

        return new DispatchRecipient
        {
            Id = Guid.NewGuid(),
            RecipientType = type,
            RecipientId = dto.Id.Trim(),
            DistanceMeters = dto.DistanceMeters,
            Status = DispatchRecipientStatus.Sent,
        };
    }

    private static IncidentStatusHistory ApplyTransition(
        Incident incident,
        IncidentStatus targetStatus,
        string actorUserId,
        string actorRole,
        string? comment)
    {
        var now = DateTime.UtcNow;
        var previousStatus = incident.Status;

        incident.Status = targetStatus;
        incident.LastUpdatedAtUtc = now;
        return new IncidentStatusHistory
        {
            Id = Guid.NewGuid(),
            IncidentId = incident.Id,
            FromStatus = previousStatus,
            ToStatus = targetStatus,
            ActorUserId = actorUserId,
            ActorRole = actorRole,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            CreatedAtUtc = now,
        };
    }

    private static void EnsureSamePayload(IncidentIdempotency record, string requestHash)
    {
        if (!string.Equals(record.RequestHash, requestHash, StringComparison.Ordinal))
        {
            throw new IdempotencyKeyConflictException();
        }
    }

    private async Task<CreateIncidentResponseDto> ResolveFromExistingIdempotencyAsync(
        string clientUserId,
        string idempotencyKey,
        string requestHash,
        CancellationToken cancellationToken)
    {
        var existingIdempotent = await _idempotencyRepository.FindAsync(clientUserId, idempotencyKey, cancellationToken);
        if (existingIdempotent is null)
        {
            throw new IdempotencyPersistenceRaceException();
        }

        EnsureSamePayload(existingIdempotent, requestHash);

        var existingIncident = await _repository.GetByIdAsync(existingIdempotent.IncidentId, cancellationToken);
        if (existingIncident is null)
        {
            throw new InvalidOperationException("Idempotency record references missing incident.");
        }

        return new CreateIncidentResponseDto
        {
            IncidentId = existingIncident.Id,
            Incident = existingIncident.ToIncidentDto(),
        };
    }

    private static IncidentIdempotency BuildIdempotencyRecord(
        string clientUserId,
        string idempotencyKey,
        string requestHash,
        Guid incidentId,
        DateTime createdAtUtc) =>
        new()
        {
            Id = Guid.NewGuid(),
            ClientUserId = clientUserId,
            IdempotencyKey = idempotencyKey,
            RequestHash = requestHash,
            IncidentId = incidentId,
            CreatedAtUtc = createdAtUtc,
            ExpiresAtUtc = createdAtUtc.Add(IdempotencyTtl),
        };

    private static string ComputeRequestHash(CreateIncidentDto request)
    {
        var normalized = string.Create(
            CultureInfo.InvariantCulture,
            $"{FormatNullable(request.DeviceTimeUtc)}|{request.AddressText?.Trim() ?? string.Empty}|{FormatLocation(request.ClientLocation)}");
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes);
    }

    private static string FormatLocation(IncidentLocationDto? location)
    {
        if (location is null)
        {
            return "null";
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{location.Lat:G17},{location.Lon:G17},{FormatNullable(location.AccuracyM)}");
    }

    private static string FormatNullable(DateTime? value) =>
        value?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? "null";

    private static string FormatNullable(double? value) =>
        value?.ToString("G17", CultureInfo.InvariantCulture) ?? "null";

    private static IncidentLocationDto? ToLocation(NetTopologySuite.Geometries.Point? point, double? accuracyM)
    {
        var (lat, lon) = GeoPointHelper.Read(point);
        if (!lat.HasValue || !lon.HasValue)
        {
            return null;
        }

        return new IncidentLocationDto
        {
            Lat = lat.Value,
            Lon = lon.Value,
            AccuracyM = accuracyM,
        };
    }
}
