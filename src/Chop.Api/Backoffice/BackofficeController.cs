using Chop.Domain.Incidents;
using Chop.Domain.Auth;
using Chop.Domain.Clients;
using Chop.Domain.Guards;
using Chop.Api.Auth;
using Chop.Application.Platform;
using Chop.Infrastructure.Persistence;
using Chop.Shared.Contracts.Backoffice;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Chop.Api.Backoffice;

[ApiController]
[Route("api")]
public sealed class BackofficeController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly BackofficePaymentsStore _paymentsStore;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditLogService _auditLogService;
    private readonly ISecurityPointAddressNormalizer _securityPointAddressNormalizer;
    private static readonly HashSet<string> AllowedBackofficeRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "HR",
        "OPERATOR",
        "MANAGER",
        "ACCOUNTANT",
        "ADMIN",
        "SUPERADMIN",
    };

    public BackofficeController(
        AppDbContext dbContext,
        IConfiguration configuration,
        BackofficePaymentsStore paymentsStore,
        IPasswordHasher passwordHasher,
        IAuditLogService auditLogService,
        ISecurityPointAddressNormalizer securityPointAddressNormalizer)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _paymentsStore = paymentsStore;
        _passwordHasher = passwordHasher;
        _auditLogService = auditLogService;
        _securityPointAddressNormalizer = securityPointAddressNormalizer;
    }

    [HttpGet("hr/guards")]
    [Authorize(Roles = "HR,OPERATOR,ADMIN,SUPERADMIN")]
    public async Task<ActionResult<IReadOnlyCollection<GuardItemDto>>> GetGuards(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] bool onShiftOnly,
        CancellationToken cancellationToken)
    {
        var guardRows = await _dbContext.UserRoles
            .AsNoTracking()
            .Where(x => x.Role == "GUARD")
            .Join(_dbContext.Users.AsNoTracking(), role => role.UserId, user => user.Id, (role, user) => user)
            .OrderBy(x => x.Login)
            .ToListAsync(cancellationToken);

        var guardIds = guardRows.Select(x => x.Id.ToString("D")).ToArray();

        var activeShifts = await _dbContext.GuardShifts
            .AsNoTracking()
            .Include(x => x.GuardGroup)
            .Include(x => x.SecurityPoint)
            .Where(x => guardIds.Contains(x.GuardUserId) && x.EndedAtUtc == null)
            .GroupBy(x => x.GuardUserId)
            .Select(g => g.OrderByDescending(x => x.StartedAtUtc).First())
            .ToDictionaryAsync(x => x.GuardUserId, cancellationToken);

        var assignments = await _dbContext.IncidentAssignments
            .AsNoTracking()
            .Where(x => x.GuardUserId != null && guardIds.Contains(x.GuardUserId) && x.Status != IncidentAssignmentStatus.Finished)
            .GroupBy(x => x.GuardUserId!)
            .ToDictionaryAsync(
                g => g.Key,
                g => new
                {
                    UpdatedAtUtc = g.Max(x => x.CreatedAtUtc),
                },
                cancellationToken);

        IEnumerable<GuardItemDto> query = guardRows.Select(user =>
        {
            var userId = user.Id.ToString("D");
            var hasShift = activeShifts.TryGetValue(userId, out var shift);
            var hasAssignment = assignments.TryGetValue(userId, out var assignmentInfo);
            var updatedAtUtc = hasShift
                ? shift!.StartedAtUtc
                : (hasAssignment ? assignmentInfo!.UpdatedAtUtc : user.CreatedAtUtc);

            return new GuardItemDto
            {
                Id = user.Id,
                CallSign = string.IsNullOrWhiteSpace(user.CallSign) ? $"G-{user.Id.ToString("N")[..4].ToUpperInvariant()}" : user.CallSign,
                FullName = string.IsNullOrWhiteSpace(user.FullName) ? user.Login : user.FullName,
                Phone = string.IsNullOrWhiteSpace(user.Phone) ? "-" : user.Phone,
                Email = string.IsNullOrWhiteSpace(user.Email) ? "-" : user.Email,
                IsActive = user.IsActive,
                OnShift = hasShift,
                AssignedPost = hasShift ? shift!.SecurityPoint?.Label : null,
                GroupName = hasShift ? shift!.GuardGroup?.Name : null,
                ShiftStartedAtUtc = hasShift ? shift!.StartedAtUtc : null,
                UpdatedAtUtc = updatedAtUtc,
            };
        });

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                x.FullName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || x.CallSign.Contains(search, StringComparison.OrdinalIgnoreCase)
                || x.Id.ToString("D").Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.IsActive);
        }
        else if (string.Equals(status, "inactive", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => !x.IsActive);
        }

        if (onShiftOnly)
        {
            query = query.Where(x => x.OnShift);
        }

        return Ok(query.OrderBy(x => x.IsActive ? 0 : 1).ThenBy(x => x.FullName).ToArray());
    }

    [HttpPost("hr/guards")]
    [Authorize(Roles = "HR,ADMIN,SUPERADMIN")]
    public async Task<ActionResult<GuardItemDto>> CreateGuard([FromBody] CreateGuardRequestDto request, CancellationToken cancellationToken)
    {
        var login = request.Login?.Trim();
        var fullName = request.FullName?.Trim();
        var callSign = request.CallSign?.Trim();
        if (string.IsNullOrWhiteSpace(login))
        {
            return BadRequest("login is required.");
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            return BadRequest("fullName is required.");
        }

        if (string.IsNullOrWhiteSpace(callSign))
        {
            return BadRequest("callSign is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("password is required.");
        }

        var exists = await _dbContext.Users.AnyAsync(x => x.Login == login, cancellationToken);
        if (exists)
        {
            return Conflict("login already exists.");
        }

        var callSignExists = await _dbContext.Users.AnyAsync(x => x.CallSign == callSign, cancellationToken);
        if (callSignExists)
        {
            return Conflict("callSign already exists.");
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Login = login,
            FullName = fullName,
            CallSign = callSign,
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            IsActive = true,
            CreatedAtUtc = now,
        };

        var credential = new UserCredential
        {
            UserId = user.Id,
            PasswordAlgo = "PBKDF2-SHA256",
            PasswordHash = _passwordHasher.Hash(request.Password),
            PasswordChangedAtUtc = now,
        };

        var role = new UserRole
        {
            UserId = user.Id,
            Role = "GUARD",
        };

        _dbContext.Users.Add(user);
        _dbContext.UserCredentials.Add(credential);
        _dbContext.UserRoles.Add(role);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.WriteAsync(
            "identity.guard.create",
            "user",
            user.Id,
            User.GetUserId(),
            "HR",
            $$"""{"login":"{{login}}"}""",
            cancellationToken);

        return Ok(new GuardItemDto
        {
            Id = user.Id,
            CallSign = user.CallSign!,
            FullName = user.FullName!,
            Phone = string.IsNullOrWhiteSpace(user.Phone) ? "-" : user.Phone,
            Email = string.IsNullOrWhiteSpace(user.Email) ? "-" : user.Email,
            IsActive = true,
            OnShift = false,
            AssignedPost = null,
            GroupName = null,
            ShiftStartedAtUtc = null,
            UpdatedAtUtc = user.CreatedAtUtc,
        });
    }

    [HttpPost("hr/guards/{guardId:guid}/toggle-active")]
    [Authorize(Roles = "HR,ADMIN,SUPERADMIN")]
    public async Task<IActionResult> ToggleGuardActive(Guid guardId, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == guardId, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        var hasGuardRole = await _dbContext.UserRoles
            .AnyAsync(x => x.UserId == guardId && x.Role == "GUARD", cancellationToken);
        if (!hasGuardRole)
        {
            return BadRequest("User is not a guard.");
        }

        user.IsActive = !user.IsActive;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPut("hr/guards/{guardId:guid}")]
    [Authorize(Roles = "HR,ADMIN,SUPERADMIN")]
    public async Task<ActionResult<GuardItemDto>> UpdateGuard(
        Guid guardId,
        [FromBody] UpdateGuardRequestDto request,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.SingleOrDefaultAsync(x => x.Id == guardId, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        var hasGuardRole = await _dbContext.UserRoles
            .AnyAsync(x => x.UserId == guardId && x.Role == "GUARD", cancellationToken);
        if (!hasGuardRole)
        {
            return BadRequest("User is not a guard.");
        }

        var fullName = request.FullName?.Trim();
        var callSign = request.CallSign?.Trim();
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return BadRequest("fullName is required.");
        }

        if (string.IsNullOrWhiteSpace(callSign))
        {
            return BadRequest("callSign is required.");
        }

        var duplicateCallSign = await _dbContext.Users
            .AnyAsync(x => x.Id != guardId && x.CallSign == callSign, cancellationToken);
        if (duplicateCallSign)
        {
            return Conflict("callSign already exists.");
        }

        user.FullName = fullName;
        user.CallSign = callSign;
        user.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        user.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.WriteAsync(
            "identity.guard.update",
            "user",
            user.Id,
            User.GetUserId(),
            User.GetHighestRole(),
            $$"""{"callSign":"{{user.CallSign}}"}""",
            cancellationToken);

        var guardUserId = user.Id.ToString("D");
        var activeShift = await _dbContext.GuardShifts
            .AsNoTracking()
            .Include(x => x.GuardGroup)
            .Include(x => x.SecurityPoint)
            .Where(x => x.GuardUserId == guardUserId && x.EndedAtUtc == null)
            .OrderByDescending(x => x.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var assignmentUpdatedAtUtc = await _dbContext.IncidentAssignments
            .AsNoTracking()
            .Where(x => x.GuardUserId == guardUserId && x.Status != IncidentAssignmentStatus.Finished)
            .Select(x => (DateTime?)x.CreatedAtUtc)
            .MaxAsync(cancellationToken);

        var updatedAtUtc = activeShift is not null
            ? activeShift.StartedAtUtc
            : (assignmentUpdatedAtUtc ?? user.CreatedAtUtc);

        return Ok(new GuardItemDto
        {
            Id = user.Id,
            CallSign = string.IsNullOrWhiteSpace(user.CallSign) ? $"G-{user.Id.ToString("N")[..4].ToUpperInvariant()}" : user.CallSign,
            FullName = string.IsNullOrWhiteSpace(user.FullName) ? user.Login : user.FullName,
            Phone = string.IsNullOrWhiteSpace(user.Phone) ? "-" : user.Phone,
            Email = string.IsNullOrWhiteSpace(user.Email) ? "-" : user.Email,
            IsActive = user.IsActive,
            OnShift = activeShift is not null,
            AssignedPost = activeShift?.SecurityPoint?.Label,
            GroupName = activeShift?.GuardGroup?.Name,
            ShiftStartedAtUtc = activeShift?.StartedAtUtc,
            UpdatedAtUtc = updatedAtUtc,
        });
    }

    [HttpGet("hr/groups")]
    [Authorize(Roles = "HR,OPERATOR,ADMIN,SUPERADMIN")]
    public async Task<ActionResult<IReadOnlyCollection<GuardGroupItemDto>>> GetGuardGroups(CancellationToken cancellationToken)
    {
        var groups = await _dbContext.GuardGroups
            .AsNoTracking()
            .Include(x => x.Members)
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var userLookup = await _dbContext.Users
            .AsNoTracking()
            .ToDictionaryAsync(
                x => x.Id.ToString("D"),
                x => string.IsNullOrWhiteSpace(x.FullName) ? x.Login : x.FullName!,
                cancellationToken);

        var result = groups.Select(group => new GuardGroupItemDto
        {
            Id = group.Id,
            Name = group.Name,
            IsActive = group.IsActive,
            MembersCount = group.Members.Count,
            CreatedAtUtc = group.CreatedAtUtc,
            Members = group.Members
                .OrderByDescending(x => x.IsCommander)
                .ThenBy(x => x.AddedAtUtc)
                .Select(member => new GuardGroupMemberItemDto
                {
                    Id = member.Id,
                    GuardUserId = member.GuardUserId,
                    GuardFullName = userLookup.TryGetValue(member.GuardUserId, out var fullName) ? fullName : member.GuardUserId,
                    IsCommander = member.IsCommander,
                    AddedAtUtc = member.AddedAtUtc,
                })
                .ToArray(),
        }).ToArray();

        return Ok(result);
    }

    [HttpPost("hr/groups")]
    [Authorize(Roles = "HR,ADMIN,SUPERADMIN")]
    public async Task<ActionResult<GuardGroupItemDto>> CreateGuardGroup([FromBody] CreateGuardGroupRequestDto request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest("name is required.");
        }

        var exists = await _dbContext.GuardGroups.AnyAsync(x => x.Name == name, cancellationToken);
        if (exists)
        {
            return Conflict("group name already exists.");
        }

        var group = new GuardGroup
        {
            Id = Guid.NewGuid(),
            Name = name,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        };

        _dbContext.GuardGroups.Add(group);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new GuardGroupItemDto
        {
            Id = group.Id,
            Name = group.Name,
            IsActive = group.IsActive,
            MembersCount = 0,
            CreatedAtUtc = group.CreatedAtUtc,
            Members = [],
        });
    }

    [HttpPost("hr/groups/{groupId:guid}/members")]
    [Authorize(Roles = "HR,ADMIN,SUPERADMIN")]
    public async Task<IActionResult> AddGuardToGroup(Guid groupId, [FromBody] AddGuardToGroupRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.GuardUserId))
        {
            return BadRequest("guardUserId is required.");
        }

        var guardUserId = request.GuardUserId.Trim();
        var group = await _dbContext.GuardGroups.SingleOrDefaultAsync(x => x.Id == groupId, cancellationToken);
        if (group is null)
        {
            return NotFound();
        }

        var parsed = Guid.TryParse(guardUserId, out var guardGuid);
        if (!parsed)
        {
            return BadRequest("guardUserId must be guid.");
        }

        var hasGuardRole = await _dbContext.UserRoles.AnyAsync(x => x.UserId == guardGuid && x.Role == "GUARD", cancellationToken);
        if (!hasGuardRole)
        {
            return BadRequest("user has no GUARD role.");
        }

        var exists = await _dbContext.GuardGroupMembers
            .AnyAsync(x => x.GuardGroupId == groupId && x.GuardUserId == guardUserId, cancellationToken);
        if (exists)
        {
            return NoContent();
        }

        _dbContext.GuardGroupMembers.Add(new GuardGroupMember
        {
            Id = Guid.NewGuid(),
            GuardGroupId = groupId,
            GuardUserId = guardUserId,
            IsCommander = request.IsCommander,
            AddedAtUtc = DateTime.UtcNow,
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("hr/groups/{groupId:guid}/members/{guardUserId}")]
    [Authorize(Roles = "HR,ADMIN,SUPERADMIN")]
    public async Task<IActionResult> RemoveGuardFromGroup(Guid groupId, string guardUserId, CancellationToken cancellationToken)
    {
        var member = await _dbContext.GuardGroupMembers
            .SingleOrDefaultAsync(x => x.GuardGroupId == groupId && x.GuardUserId == guardUserId, cancellationToken);
        if (member is null)
        {
            return NoContent();
        }

        _dbContext.GuardGroupMembers.Remove(member);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("hr/shifts/active")]
    [Authorize(Roles = "HR,OPERATOR,ADMIN,SUPERADMIN")]
    public async Task<ActionResult<IReadOnlyCollection<GuardShiftItemDto>>> GetActiveShifts(CancellationToken cancellationToken)
    {
        var shifts = await _dbContext.GuardShifts
            .AsNoTracking()
            .Include(x => x.GuardGroup)
            .Include(x => x.SecurityPoint)
            .Where(x => x.EndedAtUtc == null)
            .OrderByDescending(x => x.StartedAtUtc)
            .ToListAsync(cancellationToken);

        var guardIds = shifts.Select(x => x.GuardUserId).Distinct().ToArray();
        var guardGuids = guardIds
            .Select(x => Guid.TryParse(x, out var guid) ? guid : Guid.Empty)
            .Where(x => x != Guid.Empty)
            .ToArray();
        var userLookup = await _dbContext.Users
            .AsNoTracking()
            .Where(x => guardGuids.Contains(x.Id))
            .ToDictionaryAsync(
                x => x.Id.ToString("D"),
                x => string.IsNullOrWhiteSpace(x.FullName) ? x.Login : x.FullName!,
                cancellationToken);

        var result = shifts.Select(shift => new GuardShiftItemDto
        {
            Id = shift.Id,
            GuardUserId = shift.GuardUserId,
            GuardFullName = userLookup.TryGetValue(shift.GuardUserId, out var fullName) ? fullName : shift.GuardUserId,
            GuardGroupId = shift.GuardGroupId,
            GuardGroupName = shift.GuardGroup?.Name,
            SecurityPointId = shift.SecurityPointId,
            SecurityPointLabel = shift.SecurityPoint?.Label,
            StartedAtUtc = shift.StartedAtUtc,
            EndedAtUtc = shift.EndedAtUtc,
        }).ToArray();

        return Ok(result);
    }

    [HttpPost("hr/shifts/start")]
    [Authorize(Roles = "HR,OPERATOR,ADMIN,SUPERADMIN")]
    public async Task<ActionResult<GuardShiftItemDto>> StartShift([FromBody] StartGuardShiftRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.GuardUserId))
        {
            return BadRequest("guardUserId is required.");
        }

        var guardUserId = request.GuardUserId.Trim();
        if (!Guid.TryParse(guardUserId, out var guardGuid))
        {
            return BadRequest("guardUserId must be guid.");
        }

        var hasGuardRole = await _dbContext.UserRoles.AnyAsync(x => x.UserId == guardGuid && x.Role == "GUARD", cancellationToken);
        if (!hasGuardRole)
        {
            return BadRequest("user has no GUARD role.");
        }

        var hasActiveShift = await _dbContext.GuardShifts
            .AnyAsync(x => x.GuardUserId == guardUserId && x.EndedAtUtc == null, cancellationToken);
        if (hasActiveShift)
        {
            return Conflict("guard already has active shift.");
        }

        if (request.GuardGroupId.HasValue)
        {
            var groupExists = await _dbContext.GuardGroups.AnyAsync(x => x.Id == request.GuardGroupId.Value, cancellationToken);
            if (!groupExists)
            {
                return BadRequest("guardGroupId not found.");
            }
        }

        if (request.SecurityPointId.HasValue)
        {
            var pointExists = await _dbContext.SecurityPoints.AnyAsync(x => x.Id == request.SecurityPointId.Value, cancellationToken);
            if (!pointExists)
            {
                return BadRequest("securityPointId not found.");
            }
        }

        var shift = new GuardShift
        {
            Id = Guid.NewGuid(),
            GuardUserId = guardUserId,
            GuardGroupId = request.GuardGroupId,
            SecurityPointId = request.SecurityPointId,
            StartedAtUtc = DateTime.UtcNow,
        };

        _dbContext.GuardShifts.Add(shift);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var guardName = await _dbContext.Users
            .Where(x => x.Id == guardGuid)
            .Select(x => x.Login)
            .SingleAsync(cancellationToken);

        var groupName = request.GuardGroupId.HasValue
            ? await _dbContext.GuardGroups.Where(x => x.Id == request.GuardGroupId.Value).Select(x => x.Name).SingleOrDefaultAsync(cancellationToken)
            : null;

        var pointName = request.SecurityPointId.HasValue
            ? await _dbContext.SecurityPoints.Where(x => x.Id == request.SecurityPointId.Value).Select(x => x.Label).SingleOrDefaultAsync(cancellationToken)
            : null;

        return Ok(new GuardShiftItemDto
        {
            Id = shift.Id,
            GuardUserId = shift.GuardUserId,
            GuardFullName = guardName,
            GuardGroupId = shift.GuardGroupId,
            GuardGroupName = groupName,
            SecurityPointId = shift.SecurityPointId,
            SecurityPointLabel = pointName,
            StartedAtUtc = shift.StartedAtUtc,
            EndedAtUtc = null,
        });
    }

    [HttpPost("hr/shifts/end")]
    [Authorize(Roles = "HR,OPERATOR,ADMIN,SUPERADMIN")]
    public async Task<IActionResult> EndShift([FromBody] EndGuardShiftRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.GuardUserId))
        {
            return BadRequest("guardUserId is required.");
        }

        var guardUserId = request.GuardUserId.Trim();
        var shift = await _dbContext.GuardShifts
            .OrderByDescending(x => x.StartedAtUtc)
            .FirstOrDefaultAsync(x => x.GuardUserId == guardUserId && x.EndedAtUtc == null, cancellationToken);

        if (shift is null)
        {
            return NotFound();
        }

        shift.EndedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("admin/clients")]
    [Authorize(Roles = "ADMIN,SUPERADMIN,MANAGER")]
    public async Task<ActionResult<IReadOnlyCollection<AdminClientItemDto>>> GetClients(
        [FromQuery] string? search,
        [FromQuery] string? billing,
        [FromQuery] bool debtOnly,
        CancellationToken cancellationToken)
    {
        var canViewClientPii = await CanCurrentUserViewClientPiiAsync(cancellationToken);
        var restrictPii = !canViewClientPii;

        var profiles = await _dbContext.ClientProfiles
            .AsNoTracking()
            .Include(x => x.Phones)
            .OrderBy(x => x.FullName)
            .ToListAsync(cancellationToken);

        IEnumerable<AdminClientItemDto> query = profiles.Select(profile =>
        {
            var phone = profile.Phones.OrderByDescending(x => x.IsPrimary).Select(x => x.Phone).FirstOrDefault() ?? "-";
            return new AdminClientItemDto
            {
                Id = profile.Id,
                DisplayName = profile.FullName,
                ContactPhone = restrictPii ? MaskPhone(phone) : phone,
                Tariff = profile.Tariff,
                BillingStatus = profile.BillingStatus,
                LastPaymentAtUtc = profile.LastPaymentAtUtc,
                HasDebt = profile.HasDebt,
            };
        });

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(billing) && !string.Equals(billing, "all", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => string.Equals(x.BillingStatus, billing, StringComparison.OrdinalIgnoreCase));
        }

        if (debtOnly)
        {
            query = query.Where(x => x.HasDebt);
        }

        return Ok(query.ToArray());
    }

    [HttpGet("admin/clients/{id:guid}")]
    [Authorize(Roles = "ADMIN,SUPERADMIN,MANAGER")]
    public async Task<ActionResult<AdminClientDetailsDto>> GetClientById(Guid id, CancellationToken cancellationToken)
    {
        var canViewClientPii = await CanCurrentUserViewClientPiiAsync(cancellationToken);
        var restrictPii = !canViewClientPii;

        var profile = await _dbContext.ClientProfiles
            .AsNoTracking()
            .Include(x => x.Phones)
            .Include(x => x.Addresses)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (profile is null)
        {
            return NotFound();
        }

        var userId = Guid.TryParse(profile.UserId, out var parsedUserId) ? parsedUserId : Guid.Empty;
        var user = userId == Guid.Empty
            ? null
            : await _dbContext.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);

        return Ok(new AdminClientDetailsDto
        {
            Id = profile.Id,
            DisplayName = profile.FullName,
            Email = restrictPii ? null : user?.Email,
            Tariff = profile.Tariff,
            BillingStatus = profile.BillingStatus,
            LastPaymentAtUtc = profile.LastPaymentAtUtc,
            HasDebt = profile.HasDebt,
            Phones = profile.Phones
                .OrderByDescending(x => x.IsPrimary)
                .ThenBy(x => x.Id)
                .Select(x => new AdminClientPhoneItemDto
                {
                    Phone = restrictPii ? MaskPhone(x.Phone) : x.Phone,
                    Type = x.Type,
                    IsPrimary = x.IsPrimary,
                })
                .ToArray(),
            Addresses = profile.Addresses
                .OrderByDescending(x => x.IsPrimary)
                .ThenBy(x => x.Id)
                .Select(x => new AdminClientAddressItemDto
                {
                    Label = x.Label,
                    AddressText = x.AddressText,
                    Latitude = x.Latitude,
                    Longitude = x.Longitude,
                    IsPrimary = x.IsPrimary,
                })
                .ToArray(),
        });
    }

    [HttpGet("admin/tariffs")]
    [Authorize(Roles = "MANAGER,ADMIN,SUPERADMIN")]
    public async Task<ActionResult<IReadOnlyCollection<BillingTariffItemDto>>> GetTariffs(
        [FromQuery] bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.BillingTariffs
            .AsNoTracking()
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(x => x.IsActive);
        }

        var tariffs = await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Code)
            .Select(x => new BillingTariffItemDto
            {
                Code = x.Code,
                Name = x.Name,
                Description = x.Description,
                MonthlyFee = x.MonthlyFee,
                Currency = x.Currency,
                IsActive = x.IsActive,
                SortOrder = x.SortOrder,
                UpdatedAtUtc = x.UpdatedAtUtc,
            })
            .ToArrayAsync(cancellationToken);

        return Ok(tariffs);
    }

    [HttpPost("admin/clients")]
    [Authorize(Roles = "ADMIN,SUPERADMIN")]
    public async Task<ActionResult<CreateAdminClientResponseDto>> CreateClient(
        [FromBody] CreateAdminClientRequestDto request,
        CancellationToken cancellationToken)
    {
        var login = request.Login?.Trim();
        var fullName = request.FullName?.Trim();
        if (string.IsNullOrWhiteSpace(login))
        {
            return BadRequest("login is required.");
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            return BadRequest("fullName is required.");
        }

        var exists = await _dbContext.Users.AnyAsync(x => x.Login == login, cancellationToken);
        if (exists)
        {
            return Conflict("login already exists.");
        }

        var normalizedPhones = NormalizePhones(request.Phones, request.Phone);
        if (normalizedPhones.Any(x => string.IsNullOrWhiteSpace(x.Phone)))
        {
            return BadRequest("phone is required.");
        }

        var normalizedAddresses = NormalizeAddresses(request.Addresses, request.HomeAddress, request.HomeLatitude, request.HomeLongitude);
        if (normalizedAddresses.Any(x => string.IsNullOrWhiteSpace(x.AddressText)))
        {
            return BadRequest("addressText is required.");
        }

        foreach (var address in normalizedAddresses)
        {
            if (address.Latitude.HasValue ^ address.Longitude.HasValue)
            {
                return BadRequest("latitude and longitude must be provided together.");
            }
        }

        var tariffCode = string.IsNullOrWhiteSpace(request.Tariff) ? "STANDARD" : request.Tariff.Trim().ToUpperInvariant();
        var tariffExists = await _dbContext.BillingTariffs
            .AsNoTracking()
            .AnyAsync(x => x.Code == tariffCode, cancellationToken);
        if (!tariffExists)
        {
            return BadRequest("unknown tariff.");
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Login = login,
            FullName = fullName,
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            Phone = normalizedPhones.OrderByDescending(x => x.IsPrimary).Select(x => x.Phone).FirstOrDefault(),
            IsActive = true,
            CreatedAtUtc = now,
        };

        var role = new UserRole
        {
            UserId = user.Id,
            Role = "CLIENT",
        };

        var profile = new ClientProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id.ToString("D"),
            FullName = fullName,
            Tariff = tariffCode,
            BillingStatus = string.IsNullOrWhiteSpace(request.BillingStatus) ? "ACTIVE" : request.BillingStatus.Trim().ToUpperInvariant(),
            HasDebt = request.HasDebt,
            LastPaymentAtUtc = request.LastPaymentAtUtc?.ToUniversalTime() ?? now.Date,
        };

        foreach (var phone in normalizedPhones)
        {
            profile.Phones.Add(new ClientPhone
            {
                Id = Guid.NewGuid(),
                Phone = phone.Phone,
                Type = phone.Type,
                IsPrimary = phone.IsPrimary,
            });
        }

        foreach (var address in normalizedAddresses)
        {
            profile.Addresses.Add(new ClientAddress
            {
                Id = Guid.NewGuid(),
                Label = address.Label,
                AddressText = address.AddressText,
                Latitude = address.Latitude,
                Longitude = address.Longitude,
                IsPrimary = address.IsPrimary,
            });
        }

        var invitationToken = GenerateInvitationToken();
        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = ComputeSha256(invitationToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(7),
        };

        _dbContext.Users.Add(user);
        _dbContext.UserRoles.Add(role);
        _dbContext.ClientProfiles.Add(profile);
        _dbContext.Invitations.Add(invitation);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.WriteAsync(
            "identity.client.create",
            "client_profile",
            profile.Id,
            User.GetUserId(),
            User.GetHighestRole(),
            $$"""{"login":"{{login}}","hasInvitation":true}""",
            cancellationToken);

        return Ok(new CreateAdminClientResponseDto
        {
            ClientId = profile.Id,
            InvitationToken = invitationToken,
            InvitationExpiresAtUtc = invitation.ExpiresAtUtc,
        });
    }

    [HttpPut("admin/clients/{id:guid}")]
    [Authorize(Roles = "ADMIN,SUPERADMIN")]
    public async Task<ActionResult<AdminClientItemDto>> UpdateClient(
        Guid id,
        [FromBody] UpdateAdminClientRequestDto request,
        CancellationToken cancellationToken)
    {
        var profile = await _dbContext.ClientProfiles
            .Include(x => x.Phones)
            .Include(x => x.Addresses)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (profile is null)
        {
            return NotFound();
        }

        var fullName = request.FullName?.Trim();
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return BadRequest("fullName is required.");
        }

        var normalizedPhones = NormalizePhones(request.Phones, request.Phone);
        if (normalizedPhones.Any(x => string.IsNullOrWhiteSpace(x.Phone)))
        {
            return BadRequest("phone is required.");
        }

        var normalizedAddresses = NormalizeAddresses(request.Addresses, request.HomeAddress, request.HomeLatitude, request.HomeLongitude);
        if (normalizedAddresses.Any(x => string.IsNullOrWhiteSpace(x.AddressText)))
        {
            return BadRequest("addressText is required.");
        }

        foreach (var address in normalizedAddresses)
        {
            if (address.Latitude.HasValue ^ address.Longitude.HasValue)
            {
                return BadRequest("latitude and longitude must be provided together.");
            }
        }

        var tariffCode = string.IsNullOrWhiteSpace(request.Tariff) ? "STANDARD" : request.Tariff.Trim().ToUpperInvariant();
        var tariffExists = await _dbContext.BillingTariffs
            .AsNoTracking()
            .AnyAsync(x => x.Code == tariffCode, cancellationToken);
        if (!tariffExists)
        {
            return BadRequest("unknown tariff.");
        }

        profile.FullName = fullName;
        profile.Tariff = tariffCode;
        profile.BillingStatus = string.IsNullOrWhiteSpace(request.BillingStatus) ? "ACTIVE" : request.BillingStatus.Trim().ToUpperInvariant();
        profile.HasDebt = request.HasDebt;
        if (request.LastPaymentAtUtc.HasValue)
        {
            profile.LastPaymentAtUtc = request.LastPaymentAtUtc.Value.ToUniversalTime();
        }

        var userId = Guid.Parse(profile.UserId);
        var user = await _dbContext.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is not null)
        {
            user.FullName = fullName;
            user.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
            user.Phone = normalizedPhones.OrderByDescending(x => x.IsPrimary).Select(x => x.Phone).FirstOrDefault();
        }

        _dbContext.ClientPhones.RemoveRange(profile.Phones);
        foreach (var phone in normalizedPhones)
        {
            _dbContext.ClientPhones.Add(new ClientPhone
            {
                Id = Guid.NewGuid(),
                ClientProfileId = profile.Id,
                Phone = phone.Phone,
                Type = phone.Type,
                IsPrimary = phone.IsPrimary,
            });
        }

        _dbContext.ClientAddresses.RemoveRange(profile.Addresses);
        foreach (var address in normalizedAddresses)
        {
            _dbContext.ClientAddresses.Add(new ClientAddress
            {
                Id = Guid.NewGuid(),
                ClientProfileId = profile.Id,
                Label = address.Label,
                AddressText = address.AddressText,
                Latitude = address.Latitude,
                Longitude = address.Longitude,
                IsPrimary = address.IsPrimary,
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.WriteAsync(
            "identity.client.update",
            "client_profile",
            profile.Id,
            User.GetUserId(),
            User.GetHighestRole(),
            $$"""{"billingStatus":"{{profile.BillingStatus}}","hasDebt":{{profile.HasDebt.ToString().ToLowerInvariant()}}}""",
            cancellationToken);

        var contactPhone = await _dbContext.ClientPhones
            .AsNoTracking()
            .Where(x => x.ClientProfileId == profile.Id)
            .OrderByDescending(x => x.IsPrimary)
            .ThenBy(x => x.Id)
            .Select(x => x.Phone)
            .FirstOrDefaultAsync(cancellationToken) ?? "-";
        return Ok(new AdminClientItemDto
        {
            Id = profile.Id,
            DisplayName = profile.FullName,
            ContactPhone = contactPhone,
            Tariff = profile.Tariff,
            BillingStatus = profile.BillingStatus,
            LastPaymentAtUtc = profile.LastPaymentAtUtc,
            HasDebt = profile.HasDebt,
        });
    }

    [HttpGet("operator/forces")]
    [Authorize(Roles = "OPERATOR,ADMIN,SUPERADMIN")]
    public async Task<ActionResult<IReadOnlyCollection<OperatorForceItemDto>>> GetForces(
        [FromQuery] string? search,
        [FromQuery] string? availability,
        [FromQuery] bool onlineOnly,
        CancellationToken cancellationToken)
    {
        var guards = await _dbContext.UserRoles
            .AsNoTracking()
            .Where(x => x.Role == "GUARD")
            .Join(_dbContext.Users.AsNoTracking(), role => role.UserId, user => user.Id, (role, user) => user)
            .OrderBy(x => x.Login)
            .ToListAsync(cancellationToken);

        var guardIds = guards.Select(x => x.Id.ToString("D")).ToArray();
        var locations = await _dbContext.GuardLocations
            .AsNoTracking()
            .Where(x => guardIds.Contains(x.GuardUserId))
            .ToDictionaryAsync(x => x.GuardUserId, cancellationToken);
        var activeShifts = await _dbContext.GuardShifts
            .AsNoTracking()
            .Include(x => x.GuardGroup)
            .Include(x => x.SecurityPoint)
            .Where(x => guardIds.Contains(x.GuardUserId) && x.EndedAtUtc == null)
            .GroupBy(x => x.GuardUserId)
            .Select(g => g.OrderByDescending(x => x.StartedAtUtc).First())
            .ToDictionaryAsync(x => x.GuardUserId, cancellationToken);

        IEnumerable<OperatorForceItemDto> query = guards.Select(user =>
        {
            var userId = user.Id.ToString("D");
            var hasLocation = locations.TryGetValue(userId, out var location);
            var hasShift = activeShifts.TryGetValue(userId, out var shift);
            var isOnline = hasLocation && location!.UpdatedAtUtc >= DateTime.UtcNow.AddMinutes(-5);
            var availabilityValue = isOnline
                ? (hasShift ? "ON_DUTY" : "AVAILABLE")
                : "OFFLINE";

            return new OperatorForceItemDto
            {
                Id = user.Id,
                CallSign = string.IsNullOrWhiteSpace(user.CallSign) ? $"G-{user.Id.ToString("N")[..4].ToUpperInvariant()}" : user.CallSign,
                FullName = string.IsNullOrWhiteSpace(user.FullName) ? user.Login : user.FullName,
                Role = "GUARD",
                Unit = hasShift ? (shift!.GuardGroup?.Name ?? "Shift") : "Crew",
                Post = hasShift ? (shift!.SecurityPoint?.Label ?? "-") : "-",
                Phone = "-",
                Radio = "-",
                Availability = availabilityValue,
                IsOnline = isOnline,
                LastLocationAtUtc = hasLocation ? location!.UpdatedAtUtc : user.CreatedAtUtc,
            };
        });

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                x.FullName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || x.CallSign.Contains(search, StringComparison.OrdinalIgnoreCase)
                || x.Unit.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(availability) && !string.Equals(availability, "all", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => string.Equals(x.Availability, availability, StringComparison.OrdinalIgnoreCase));
        }

        if (onlineOnly)
        {
            query = query.Where(x => x.IsOnline);
        }

        return Ok(query.OrderBy(x => x.Availability == "AVAILABLE" ? 0 : 1).ThenBy(x => x.CallSign).ToArray());
    }

    [HttpGet("operator/points")]
    [Authorize(Roles = "OPERATOR,HR,ADMIN,SUPERADMIN")]
    public async Task<ActionResult<IReadOnlyCollection<OperatorPointItemDto>>> GetPoints(
        [FromQuery] string? search,
        [FromQuery] string? type,
        [FromQuery] bool includeInactive,
        CancellationToken cancellationToken)
    {
        var pointsQuery = _dbContext.SecurityPoints
            .AsNoTracking()
            .AsQueryable();

        if (!includeInactive)
        {
            pointsQuery = pointsQuery.Where(x => x.IsActive);
        }

        var points = await pointsQuery
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken);

        var pointIds = points.Select(x => x.Id).ToArray();
        var activeForcesMap = await _dbContext.GuardShifts
            .AsNoTracking()
            .Where(x => x.EndedAtUtc == null && x.SecurityPointId != null && pointIds.Contains(x.SecurityPointId.Value))
            .GroupBy(x => x.SecurityPointId!.Value)
            .ToDictionaryAsync(x => x.Key, x => x.Count(), cancellationToken);

        IEnumerable<OperatorPointItemDto> query = points.Select(point => new OperatorPointItemDto
        {
            Id = point.Id,
            Code = point.Code,
            Label = point.Label,
            Type = point.Type,
            Address = point.Address,
            Latitude = point.Latitude,
            Longitude = point.Longitude,
            IsActive = point.IsActive,
            ShiftStatus = activeForcesMap.TryGetValue(point.Id, out var activeForces) && activeForces > 0
                ? "ON_DUTY"
                : "LOW_STAFFED",
            ActiveForces = activeForcesMap.TryGetValue(point.Id, out var forces) ? forces : 0,
            LastEventAtUtc = point.CreatedAtUtc,
        });

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                x.Label.Contains(search, StringComparison.OrdinalIgnoreCase)
                || x.Code.Contains(search, StringComparison.OrdinalIgnoreCase)
                || x.Address.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(type) && !string.Equals(type, "all", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => string.Equals(x.Type, type, StringComparison.OrdinalIgnoreCase));
        }

        return Ok(query.ToArray());
    }

    [HttpPost("operator/points")]
    [Authorize(Roles = "OPERATOR,HR,ADMIN,SUPERADMIN")]
    public async Task<ActionResult<OperatorPointItemDto>> CreatePoint([FromBody] CreateSecurityPointRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest("code is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Label))
        {
            return BadRequest("label is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Address))
        {
            return BadRequest("address is required.");
        }

        var coordinateValidationError = ValidateCoordinates(request.Latitude, request.Longitude);
        if (coordinateValidationError is not null)
        {
            return BadRequest(coordinateValidationError);
        }

        var code = request.Code.Trim().ToUpperInvariant();
        var exists = await _dbContext.SecurityPoints.AnyAsync(x => x.Code == code, cancellationToken);
        if (exists)
        {
            return Conflict("point code already exists.");
        }

        var normalizedAddress = await _securityPointAddressNormalizer.NormalizeAsync(
            request.Address,
            request.Latitude,
            request.Longitude,
            cancellationToken);

        var now = DateTime.UtcNow;
        var point = new SecurityPoint
        {
            Id = Guid.NewGuid(),
            Code = code,
            Label = request.Label.Trim(),
            Type = string.IsNullOrWhiteSpace(request.Type) ? "POST" : request.Type.Trim().ToUpperInvariant(),
            Address = normalizedAddress.Address,
            Latitude = normalizedAddress.Latitude,
            Longitude = normalizedAddress.Longitude,
            IsActive = true,
            CreatedAtUtc = now,
        };

        _dbContext.SecurityPoints.Add(point);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new OperatorPointItemDto
        {
            Id = point.Id,
            Code = point.Code,
            Label = point.Label,
            Type = point.Type,
            Address = point.Address,
            Latitude = point.Latitude,
            Longitude = point.Longitude,
            IsActive = point.IsActive,
            ShiftStatus = "LOW_STAFFED",
            ActiveForces = 0,
            LastEventAtUtc = point.CreatedAtUtc,
        });
    }

    [HttpPut("operator/points/{id:guid}")]
    [Authorize(Roles = "OPERATOR,HR,ADMIN,SUPERADMIN")]
    public async Task<ActionResult<OperatorPointItemDto>> UpdatePoint(
        Guid id,
        [FromBody] UpdateSecurityPointRequestDto request,
        CancellationToken cancellationToken)
    {
        var point = await _dbContext.SecurityPoints.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (point is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest("code is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Label))
        {
            return BadRequest("label is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Address))
        {
            return BadRequest("address is required.");
        }

        var coordinateValidationError = ValidateCoordinates(request.Latitude, request.Longitude);
        if (coordinateValidationError is not null)
        {
            return BadRequest(coordinateValidationError);
        }

        var code = request.Code.Trim().ToUpperInvariant();
        var duplicateCodeExists = await _dbContext.SecurityPoints.AnyAsync(x => x.Id != id && x.Code == code, cancellationToken);
        if (duplicateCodeExists)
        {
            return Conflict("point code already exists.");
        }

        var normalizedAddress = await _securityPointAddressNormalizer.NormalizeAsync(
            request.Address,
            request.Latitude,
            request.Longitude,
            cancellationToken);

        point.Code = code;
        point.Label = request.Label.Trim();
        point.Type = string.IsNullOrWhiteSpace(request.Type) ? "POST" : request.Type.Trim().ToUpperInvariant();
        point.Address = normalizedAddress.Address;
        point.Latitude = normalizedAddress.Latitude;
        point.Longitude = normalizedAddress.Longitude;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.WriteAsync(
            "security.point.update",
            "security_point",
            point.Id,
            User.GetUserId(),
            User.GetHighestRole(),
            $$"""{"code":"{{point.Code}}","isActive":{{point.IsActive.ToString().ToLowerInvariant()}}}""",
            cancellationToken);

        var activeForces = await _dbContext.GuardShifts
            .AsNoTracking()
            .CountAsync(x => x.EndedAtUtc == null && x.SecurityPointId == point.Id, cancellationToken);

        return Ok(new OperatorPointItemDto
        {
            Id = point.Id,
            Code = point.Code,
            Label = point.Label,
            Type = point.Type,
            Address = point.Address,
            Latitude = point.Latitude,
            Longitude = point.Longitude,
            IsActive = point.IsActive,
            ShiftStatus = activeForces > 0 ? "ON_DUTY" : "LOW_STAFFED",
            ActiveForces = activeForces,
            LastEventAtUtc = point.CreatedAtUtc,
        });
    }

    [HttpPost("operator/points/{id:guid}/toggle-active")]
    [Authorize(Roles = "OPERATOR,HR,ADMIN,SUPERADMIN")]
    public async Task<IActionResult> TogglePointActive(Guid id, CancellationToken cancellationToken)
    {
        var point = await _dbContext.SecurityPoints.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (point is null)
        {
            return NotFound();
        }

        point.IsActive = !point.IsActive;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.WriteAsync(
            "security.point.toggle-active",
            "security_point",
            point.Id,
            User.GetUserId(),
            User.GetHighestRole(),
            $$"""{"isActive":{{point.IsActive.ToString().ToLowerInvariant()}}}""",
            cancellationToken);

        return NoContent();
    }

    private static string? ValidateCoordinates(double? latitude, double? longitude)
    {
        if (latitude.HasValue ^ longitude.HasValue)
        {
            return "latitude and longitude must be provided together.";
        }

        if (!latitude.HasValue)
        {
            return null;
        }

        if (latitude.Value is < -90 or > 90)
        {
            return "latitude must be between -90 and 90.";
        }

        if (longitude!.Value is < -180 or > 180)
        {
            return "longitude must be between -180 and 180.";
        }

        return null;
    }

    private async Task<bool> CanCurrentUserViewClientPiiAsync(CancellationToken cancellationToken)
    {
        if (User.IsInRole("SUPERADMIN") || User.IsInRole("ADMIN"))
        {
            return true;
        }

        if (!User.IsInRole("MANAGER"))
        {
            return false;
        }

        var actorUserId = User.GetUserId();
        if (!Guid.TryParse(actorUserId, out var actorGuid))
        {
            return false;
        }

        return await _dbContext.Users
            .AsNoTracking()
            .Where(x => x.Id == actorGuid)
            .Select(x => x.CanViewClientPii)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static string MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return "-";
        }

        var digits = phone.Where(char.IsDigit).ToArray();
        if (digits.Length <= 4)
        {
            return "****";
        }

        var tail = new string(digits[^4..]);
        return $"***-***-{tail}";
    }

    private static IReadOnlyCollection<AdminClientPhoneInputDto> NormalizePhones(
        IReadOnlyCollection<AdminClientPhoneInputDto>? phones,
        string? fallbackPhone)
    {
        var source = phones ?? [];
        var normalized = source
            .Where(x => !string.IsNullOrWhiteSpace(x.Phone))
            .Select(x => new AdminClientPhoneInputDto
            {
                Phone = x.Phone.Trim(),
                Type = string.IsNullOrWhiteSpace(x.Type) ? "OTHER" : x.Type.Trim().ToUpperInvariant(),
                IsPrimary = x.IsPrimary,
            })
            .DistinctBy(x => x.Phone, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0 && !string.IsNullOrWhiteSpace(fallbackPhone))
        {
            normalized.Add(new AdminClientPhoneInputDto
            {
                Phone = fallbackPhone.Trim(),
                Type = "PRIMARY",
                IsPrimary = true,
            });
        }

        if (normalized.Count > 0 && normalized.All(x => !x.IsPrimary))
        {
            normalized[0].IsPrimary = true;
        }

        return normalized;
    }

    private static IReadOnlyCollection<AdminClientAddressInputDto> NormalizeAddresses(
        IReadOnlyCollection<AdminClientAddressInputDto>? addresses,
        string? fallbackHomeAddress,
        double? fallbackHomeLatitude,
        double? fallbackHomeLongitude)
    {
        var source = addresses ?? [];
        var normalized = source
            .Where(x => !string.IsNullOrWhiteSpace(x.AddressText))
            .Select(x => new AdminClientAddressInputDto
            {
                Label = string.IsNullOrWhiteSpace(x.Label) ? "OTHER" : x.Label.Trim().ToUpperInvariant(),
                AddressText = x.AddressText.Trim(),
                Latitude = x.Latitude,
                Longitude = x.Longitude,
                IsPrimary = x.IsPrimary,
            })
            .ToList();

        if (normalized.Count == 0 && !string.IsNullOrWhiteSpace(fallbackHomeAddress))
        {
            normalized.Add(new AdminClientAddressInputDto
            {
                Label = "HOME",
                AddressText = fallbackHomeAddress.Trim(),
                Latitude = fallbackHomeLatitude,
                Longitude = fallbackHomeLongitude,
                IsPrimary = true,
            });
        }

        if (normalized.Count > 0 && normalized.All(x => !x.IsPrimary))
        {
            normalized[0].IsPrimary = true;
        }

        return normalized;
    }

    [HttpGet("superadmin/settings")]
    [Authorize(Roles = "SUPERADMIN")]
    public ActionResult<IReadOnlyCollection<SuperAdminSettingItemDto>> GetSettings([FromQuery] string? scope)
    {
        var settings = new[]
        {
            new SuperAdminSettingItemDto
            {
                Key = "auth.access_token_ttl_minutes",
                Value = _configuration["Jwt:AccessTokenTtlMinutes"] ?? "15",
                Scope = "security",
                UpdatedAtUtc = DateTime.UtcNow,
            },
            new SuperAdminSettingItemDto
            {
                Key = "auth.refresh_token_ttl_days",
                Value = _configuration["Jwt:RefreshTokenTtlDays"] ?? "30",
                Scope = "security",
                UpdatedAtUtc = DateTime.UtcNow,
            },
            new SuperAdminSettingItemDto
            {
                Key = "incidents.dedup_window_seconds",
                Value = "60",
                Scope = "incidents",
                UpdatedAtUtc = DateTime.UtcNow,
            },
            new SuperAdminSettingItemDto
            {
                Key = "notifications.default_retry_limit",
                Value = _configuration["Notifications:Outbox:MaxAttempts"] ?? "5",
                Scope = "notifications",
                UpdatedAtUtc = DateTime.UtcNow,
            },
        };

        var filtered = settings.Where(x => string.IsNullOrWhiteSpace(scope) || scope == "all" || x.Scope.Equals(scope, StringComparison.OrdinalIgnoreCase));
        return Ok(filtered.ToArray());
    }

    [HttpPost("superadmin/tariffs")]
    [Authorize(Roles = "SUPERADMIN")]
    public async Task<ActionResult<BillingTariffItemDto>> CreateTariff(
        [FromBody] UpsertBillingTariffRequestDto request,
        CancellationToken cancellationToken)
    {
        var code = request.Code?.Trim().ToUpperInvariant();
        var name = request.Name?.Trim();
        var currency = string.IsNullOrWhiteSpace(request.Currency) ? "KZT" : request.Currency.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code))
        {
            return BadRequest("code is required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest("name is required.");
        }

        var exists = await _dbContext.BillingTariffs.AsNoTracking().AnyAsync(x => x.Code == code, cancellationToken);
        if (exists)
        {
            return Conflict("tariff already exists.");
        }

        var tariff = new BillingTariff
        {
            Code = code,
            Name = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            MonthlyFee = request.MonthlyFee,
            Currency = currency,
            IsActive = request.IsActive,
            SortOrder = request.SortOrder,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        _dbContext.BillingTariffs.Add(tariff);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.WriteAsync(
            "billing.tariff.create",
            "billing_tariff",
            null,
            User.GetUserId(),
            "SUPERADMIN",
            $$"""{"code":"{{tariff.Code}}"}""",
            cancellationToken);

        return Ok(ToTariffDto(tariff));
    }

    [HttpPut("superadmin/tariffs/{code}")]
    [Authorize(Roles = "SUPERADMIN")]
    public async Task<ActionResult<BillingTariffItemDto>> UpdateTariff(
        string code,
        [FromBody] UpsertBillingTariffRequestDto request,
        CancellationToken cancellationToken)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();
        var tariff = await _dbContext.BillingTariffs.SingleOrDefaultAsync(x => x.Code == normalizedCode, cancellationToken);
        if (tariff is null)
        {
            return NotFound();
        }

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest("name is required.");
        }

        tariff.Name = name;
        tariff.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        tariff.MonthlyFee = request.MonthlyFee;
        tariff.Currency = string.IsNullOrWhiteSpace(request.Currency) ? "KZT" : request.Currency.Trim().ToUpperInvariant();

        if (!request.IsActive && tariff.IsActive)
        {
            var usedByClients = await _dbContext.ClientProfiles
                .AsNoTracking()
                .AnyAsync(x => x.Tariff == tariff.Code, cancellationToken);
            if (usedByClients)
            {
                return BadRequest("tariff is assigned to clients and cannot be deactivated.");
            }
        }

        tariff.IsActive = request.IsActive;
        tariff.SortOrder = request.SortOrder;
        tariff.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.WriteAsync(
            "billing.tariff.update",
            "billing_tariff",
            null,
            User.GetUserId(),
            "SUPERADMIN",
            $$"""{"code":"{{tariff.Code}}","isActive":{{tariff.IsActive.ToString().ToLowerInvariant()}}}""",
            cancellationToken);

        return Ok(ToTariffDto(tariff));
    }

    [HttpDelete("superadmin/tariffs/{code}")]
    [Authorize(Roles = "SUPERADMIN")]
    public async Task<IActionResult> DeleteTariff(string code, CancellationToken cancellationToken)
    {
        var normalizedCode = code.Trim().ToUpperInvariant();
        var tariff = await _dbContext.BillingTariffs.SingleOrDefaultAsync(x => x.Code == normalizedCode, cancellationToken);
        if (tariff is null)
        {
            return NotFound();
        }

        var usedByClients = await _dbContext.ClientProfiles
            .AsNoTracking()
            .AnyAsync(x => x.Tariff == tariff.Code, cancellationToken);
        if (usedByClients)
        {
            return BadRequest("tariff is assigned to clients and cannot be deleted.");
        }

        _dbContext.BillingTariffs.Remove(tariff);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.WriteAsync(
            "billing.tariff.delete",
            "billing_tariff",
            null,
            User.GetUserId(),
            "SUPERADMIN",
            $$"""{"code":"{{tariff.Code}}"}""",
            cancellationToken);

        return NoContent();
    }

    [HttpGet("superadmin/audit")]
    [Authorize(Roles = "SUPERADMIN")]
    public async Task<ActionResult<IReadOnlyCollection<SuperAdminAuditItemDto>>> GetAudit([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var entries = await _dbContext.AuditLogEntries
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken);

        IEnumerable<SuperAdminAuditItemDto> query = entries.Select(x => new SuperAdminAuditItemDto
        {
            Id = x.Id,
            AtUtc = x.CreatedAtUtc,
            Actor = x.ActorUserId ?? "system",
            Action = x.Action,
            Target = x.EntityId.HasValue ? $"{x.EntityType}/{x.EntityId.Value:D}" : x.EntityType,
            Result = string.IsNullOrWhiteSpace(x.ChangesJson) ? "SUCCESS" : "SUCCESS",
        });

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                x.Actor.Contains(search, StringComparison.OrdinalIgnoreCase)
                || x.Action.Contains(search, StringComparison.OrdinalIgnoreCase)
                || x.Target.Contains(search, StringComparison.OrdinalIgnoreCase)
                || x.Result.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return Ok(query.ToArray());
    }

    [HttpGet("superadmin/users")]
    [Authorize(Roles = "SUPERADMIN")]
    public async Task<ActionResult<IReadOnlyCollection<BackofficeUserItemDto>>> GetBackofficeUsers(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] bool? active,
        CancellationToken cancellationToken)
    {
        var userRows = await _dbContext.Users
            .AsNoTracking()
            .Include(x => x.Roles)
            .OrderBy(x => x.Login)
            .ToListAsync(cancellationToken);

        IEnumerable<BackofficeUserItemDto> query = userRows
            .Select(x => new BackofficeUserItemDto
            {
                Id = x.Id,
                Login = x.Login,
                Email = x.Email,
                Phone = x.Phone,
                IsActive = x.IsActive,
                CanViewClientPii = x.CanViewClientPii,
                CreatedAtUtc = x.CreatedAtUtc,
                Roles = x.Roles.Select(r => r.Role).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(r => r).ToArray(),
            })
            .Where(x => x.Roles.Any(r => AllowedBackofficeRoles.Contains(r)));

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x =>
                x.Login.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(x.Email) && x.Email.Contains(search, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrWhiteSpace(x.Phone) && x.Phone.Contains(search, StringComparison.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(x => x.Roles.Contains(role, StringComparer.OrdinalIgnoreCase));
        }

        if (active.HasValue)
        {
            query = query.Where(x => x.IsActive == active.Value);
        }

        return Ok(query.ToArray());
    }

    [HttpPost("superadmin/users")]
    [Authorize(Roles = "SUPERADMIN")]
    public async Task<ActionResult<BackofficeUserItemDto>> CreateBackofficeUser(
        [FromBody] CreateBackofficeUserRequestDto request,
        CancellationToken cancellationToken)
    {
        var login = request.Login.Trim();
        if (string.IsNullOrWhiteSpace(login))
        {
            return BadRequest("login is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("password is required.");
        }

        var roles = request.Roles
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (roles.Length == 0)
        {
            return BadRequest("at least one role is required.");
        }

        if (roles.Any(x => !AllowedBackofficeRoles.Contains(x)))
        {
            return BadRequest("unsupported role.");
        }

        var exists = await _dbContext.Users
            .AnyAsync(x => x.Login == login, cancellationToken);
        if (exists)
        {
            return Conflict("login already exists.");
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Login = login,
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            IsActive = true,
            CanViewClientPii = request.CanViewClientPii,
            CreatedAtUtc = now,
        };

        var credential = new UserCredential
        {
            UserId = user.Id,
            PasswordAlgo = "PBKDF2-SHA256",
            PasswordHash = _passwordHasher.Hash(request.Password),
            PasswordChangedAtUtc = now,
        };

        var userRoles = roles.Select(x => new UserRole
        {
            UserId = user.Id,
            Role = x,
        }).ToArray();

        _dbContext.Users.Add(user);
        _dbContext.UserCredentials.Add(credential);
        _dbContext.UserRoles.AddRange(userRoles);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.WriteAsync(
            "identity.user.create",
            "user",
            user.Id,
            User.GetUserId(),
            "SUPERADMIN",
            $$"""{"login":"{{user.Login}}","roles":[{{string.Join(',', roles.Select(r => $"\"{r}\""))}}]}""",
            cancellationToken);

        return Ok(new BackofficeUserItemDto
        {
            Id = user.Id,
            Login = user.Login,
            Email = user.Email,
            Phone = user.Phone,
            IsActive = user.IsActive,
            CanViewClientPii = user.CanViewClientPii,
            CreatedAtUtc = user.CreatedAtUtc,
            Roles = roles,
        });
    }

    [HttpPost("superadmin/users/{userId:guid}/roles/add")]
    [Authorize(Roles = "SUPERADMIN")]
    public async Task<IActionResult> AddRole(Guid userId, [FromBody] ChangeBackofficeUserRoleRequestDto request, CancellationToken cancellationToken)
    {
        var role = request.Role.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(role) || !AllowedBackofficeRoles.Contains(role))
        {
            return BadRequest("unsupported role.");
        }

        var user = await _dbContext.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        var exists = await _dbContext.UserRoles.AnyAsync(x => x.UserId == userId && x.Role == role, cancellationToken);
        if (!exists)
        {
            _dbContext.UserRoles.Add(new UserRole { UserId = userId, Role = role });
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _auditLogService.WriteAsync(
                "identity.user.role.add",
                "user",
                userId,
                User.GetUserId(),
                "SUPERADMIN",
                $$"""{"role":"{{role}}"}""",
                cancellationToken);
        }

        return NoContent();
    }

    [HttpPost("superadmin/users/{userId:guid}/roles/remove")]
    [Authorize(Roles = "SUPERADMIN")]
    public async Task<IActionResult> RemoveRole(Guid userId, [FromBody] ChangeBackofficeUserRoleRequestDto request, CancellationToken cancellationToken)
    {
        var role = request.Role.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(role) || !AllowedBackofficeRoles.Contains(role))
        {
            return BadRequest("unsupported role.");
        }

        var user = await _dbContext.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        var actorUserId = User.GetUserId();
        if (string.Equals(role, "SUPERADMIN", StringComparison.OrdinalIgnoreCase))
        {
            if (Guid.TryParse(actorUserId, out var actorId) && actorId == userId)
            {
                return BadRequest("cannot remove SUPERADMIN role from self.");
            }

            var superAdminsCount = await _dbContext.UserRoles.CountAsync(x => x.Role == "SUPERADMIN", cancellationToken);
            if (superAdminsCount <= 1)
            {
                return BadRequest("cannot remove last SUPERADMIN.");
            }
        }

        var mapping = await _dbContext.UserRoles.SingleOrDefaultAsync(x => x.UserId == userId && x.Role == role, cancellationToken);
        if (mapping is null)
        {
            return NoContent();
        }

        var rolesCount = await _dbContext.UserRoles.CountAsync(x => x.UserId == userId, cancellationToken);
        if (rolesCount <= 1)
        {
            return BadRequest("cannot remove last role.");
        }

        _dbContext.UserRoles.Remove(mapping);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditLogService.WriteAsync(
            "identity.user.role.remove",
            "user",
            userId,
            actorUserId,
            "SUPERADMIN",
            $$"""{"role":"{{role}}"}""",
            cancellationToken);

        return NoContent();
    }

    [HttpPost("superadmin/users/{userId:guid}/toggle-active")]
    [Authorize(Roles = "SUPERADMIN")]
    public async Task<IActionResult> ToggleUserActive(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        var actorUserId = User.GetUserId();
        if (Guid.TryParse(actorUserId, out var actorId) && actorId == userId && user.IsActive)
        {
            return BadRequest("cannot deactivate self.");
        }

        user.IsActive = !user.IsActive;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditLogService.WriteAsync(
            "identity.user.toggle-active",
            "user",
            userId,
            actorUserId,
            "SUPERADMIN",
            $$"""{"isActive":{{user.IsActive.ToString().ToLowerInvariant()}}}""",
            cancellationToken);
        return NoContent();
    }

    [HttpPost("superadmin/users/{userId:guid}/toggle-client-pii")]
    [Authorize(Roles = "SUPERADMIN")]
    public async Task<IActionResult> ToggleUserClientPiiAccess(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        user.CanViewClientPii = !user.CanViewClientPii;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.WriteAsync(
            "identity.user.pii.toggle",
            "user",
            userId,
            User.GetUserId(),
            "SUPERADMIN",
            $$"""{"canViewClientPii":{{user.CanViewClientPii.ToString().ToLowerInvariant()}}}""",
            cancellationToken);

        return NoContent();
    }

    [HttpPost("admin/payments/import")]
    [Authorize(Roles = "ADMIN,SUPERADMIN,ACCOUNTANT")]
    [RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<ActionResult<PaymentImportItemDto>> ImportPayments([FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("File is required.");
        }

        await using var stream = file.OpenReadStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        var payload = ms.ToArray();
        if (payload.Length == 0)
        {
            return BadRequest("File is empty.");
        }

        try
        {
            var import = await _paymentsStore.CreateDraftAsync(
                file.FileName,
                payload,
                User.GetUserId(),
                User.GetHighestRole(),
                cancellationToken);
            return Ok(import);
        }
        catch (Payments.Payment1CParseException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("admin/payments/imports")]
    [Authorize(Roles = "ADMIN,SUPERADMIN,ACCOUNTANT")]
    public async Task<ActionResult<IReadOnlyCollection<PaymentImportItemDto>>> GetPaymentImports(CancellationToken cancellationToken) =>
        Ok(await _paymentsStore.GetImportsAsync(cancellationToken));

    [HttpGet("admin/payments/imports/{importId:guid}")]
    [Authorize(Roles = "ADMIN,SUPERADMIN,ACCOUNTANT")]
    public async Task<ActionResult<PaymentImportItemDto>> GetPaymentImport(Guid importId, CancellationToken cancellationToken)
    {
        var item = await _paymentsStore.GetImportAsync(importId, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("admin/payments/imports/{importId:guid}/rows")]
    [Authorize(Roles = "ADMIN,SUPERADMIN,ACCOUNTANT")]
    public async Task<ActionResult<IReadOnlyCollection<PaymentImportRowItemDto>>> GetPaymentImportRows(Guid importId, CancellationToken cancellationToken) =>
        Ok(await _paymentsStore.GetRowsAsync(importId, cancellationToken));

    [HttpPost("admin/payments/imports/{importId:guid}/apply")]
    [Authorize(Roles = "ADMIN,SUPERADMIN,ACCOUNTANT")]
    public async Task<IActionResult> ApplyPaymentImport(Guid importId, CancellationToken cancellationToken)
    {
        var applied = await _paymentsStore.ApplyAsync(importId, User.GetUserId(), User.GetHighestRole(), cancellationToken);
        return applied ? NoContent() : NotFound();
    }

    [HttpPost("admin/payments/imports/{importId:guid}/rows/{rowId:guid}/match")]
    [Authorize(Roles = "ADMIN,SUPERADMIN,ACCOUNTANT")]
    public async Task<IActionResult> MatchPaymentImportRow(Guid importId, Guid rowId, [FromBody] ManualMatchRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ClientDisplayName) && string.IsNullOrWhiteSpace(request.ClientUserId))
        {
            return BadRequest("clientDisplayName or clientUserId is required.");
        }

        var matched = await _paymentsStore.ManualMatchAsync(
            importId,
            rowId,
            request.ClientUserId,
            request.ClientDisplayName,
            User.GetUserId(),
            User.GetHighestRole(),
            cancellationToken);
        return matched ? NoContent() : NotFound();
    }

    private static string GenerateInvitationToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string ComputeSha256(string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static BillingTariffItemDto ToTariffDto(BillingTariff tariff) =>
        new()
        {
            Code = tariff.Code,
            Name = tariff.Name,
            Description = tariff.Description,
            MonthlyFee = tariff.MonthlyFee,
            Currency = tariff.Currency,
            IsActive = tariff.IsActive,
            SortOrder = tariff.SortOrder,
            UpdatedAtUtc = tariff.UpdatedAtUtc,
        };
}

