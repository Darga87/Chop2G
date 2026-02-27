using Chop.Api.Auth;
using Chop.Application.Platform;
using Chop.Domain.Clients;
using Chop.Infrastructure.Persistence;
using Chop.Shared.Contracts.Clients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chop.Api.Clients;

[ApiController]
[Route("api/clients")]
public sealed class ClientsController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IAuditLogService _auditLogService;

    public ClientsController(AppDbContext dbContext, IAuditLogService auditLogService)
    {
        _dbContext = dbContext;
        _auditLogService = auditLogService;
    }

    [HttpGet("me")]
    [Authorize(Roles = "CLIENT")]
    public async Task<ActionResult<ClientProfileDto>> GetMe(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var profile = await _dbContext.ClientProfiles
            .AsNoTracking()
            .Include(x => x.Phones)
            .Include(x => x.Addresses)
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (profile is null)
        {
            return NotFound();
        }

        var userGuid = Guid.TryParse(userId, out var parsedUserId) ? parsedUserId : Guid.Empty;
        var user = userGuid == Guid.Empty
            ? null
            : await _dbContext.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userGuid, cancellationToken);

        return Ok(ToDto(profile, user?.Email));
    }

    [HttpPut("me")]
    [Authorize(Roles = "CLIENT")]
    public async Task<ActionResult<ClientProfileDto>> UpdateMe([FromBody] UpdateClientProfileDto request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var fullName = request.FullName?.Trim();
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return BadRequest("fullName is required.");
        }

        var phones = NormalizePhones(request.Phones);
        var addresses = NormalizeAddresses(request.Addresses);
        foreach (var address in addresses)
        {
            if (address.Latitude.HasValue ^ address.Longitude.HasValue)
            {
                return BadRequest("latitude and longitude must be provided together.");
            }
        }

        var profile = await _dbContext.ClientProfiles
            .Include(x => x.Phones)
            .Include(x => x.Addresses)
            .SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (profile is null)
        {
            return NotFound();
        }

        profile.FullName = fullName;

        var userGuid = Guid.TryParse(userId, out var parsedUserId) ? parsedUserId : Guid.Empty;
        var user = userGuid == Guid.Empty
            ? null
            : await _dbContext.Users.SingleOrDefaultAsync(x => x.Id == userGuid, cancellationToken);
        if (user is not null)
        {
            user.FullName = fullName;
            user.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
            user.Phone = phones.OrderByDescending(x => x.IsPrimary).Select(x => x.Phone).FirstOrDefault();
        }

        _dbContext.ClientPhones.RemoveRange(profile.Phones);
        foreach (var phone in phones)
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
        foreach (var address in addresses)
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
            "client.profile.update",
            "client_profile",
            profile.Id,
            userId,
            "CLIENT",
            $$"""{"phones":{{phones.Count}},"addresses":{{addresses.Count}}}""",
            cancellationToken);

        var reloaded = await _dbContext.ClientProfiles
            .AsNoTracking()
            .Include(x => x.Phones)
            .Include(x => x.Addresses)
            .SingleAsync(x => x.Id == profile.Id, cancellationToken);
        var responseEmail = user?.Email;
        return Ok(ToDto(reloaded, responseEmail));
    }

    private static ClientProfileDto ToDto(ClientProfile profile, string? email) =>
        new()
        {
            UserId = profile.UserId,
            FullName = profile.FullName,
            Email = email,
            Phones = profile.Phones
                .OrderByDescending(x => x.IsPrimary)
                .ThenBy(x => x.Id)
                .Select(x => new ClientProfilePhoneDto
                {
                    Phone = x.Phone,
                    Type = x.Type,
                    IsPrimary = x.IsPrimary,
                })
                .ToArray(),
            Addresses = profile.Addresses
                .OrderByDescending(x => x.IsPrimary)
                .ThenBy(x => x.Id)
                .Select(x => new ClientProfileAddressDto
                {
                    Label = x.Label,
                    AddressText = x.AddressText,
                    Latitude = x.Latitude,
                    Longitude = x.Longitude,
                    IsPrimary = x.IsPrimary,
                })
                .ToArray(),
        };

    private static List<UpdateClientPhoneDto> NormalizePhones(IReadOnlyCollection<UpdateClientPhoneDto>? phones)
    {
        var source = phones ?? [];
        var normalized = source
            .Where(x => !string.IsNullOrWhiteSpace(x.Phone))
            .Select(x => new UpdateClientPhoneDto
            {
                Phone = x.Phone.Trim(),
                Type = string.IsNullOrWhiteSpace(x.Type) ? "OTHER" : x.Type.Trim().ToUpperInvariant(),
                IsPrimary = x.IsPrimary,
            })
            .DistinctBy(x => x.Phone, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count > 0 && normalized.All(x => !x.IsPrimary))
        {
            normalized[0].IsPrimary = true;
        }

        return normalized;
    }

    private static List<UpdateClientAddressDto> NormalizeAddresses(IReadOnlyCollection<UpdateClientAddressDto>? addresses)
    {
        var source = addresses ?? [];
        var normalized = source
            .Where(x => !string.IsNullOrWhiteSpace(x.AddressText))
            .Select(x => new UpdateClientAddressDto
            {
                Label = string.IsNullOrWhiteSpace(x.Label) ? "OTHER" : x.Label.Trim().ToUpperInvariant(),
                AddressText = x.AddressText.Trim(),
                Latitude = x.Latitude,
                Longitude = x.Longitude,
                IsPrimary = x.IsPrimary,
            })
            .ToList();

        if (normalized.Count > 0 && normalized.All(x => !x.IsPrimary))
        {
            normalized[0].IsPrimary = true;
        }

        return normalized;
    }
}
