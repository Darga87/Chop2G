using Chop.Application.Platform;
using Chop.Domain.Auth;
using Chop.Shared.Contracts.Auth;
using Chop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Cryptography;
using System.Text;

namespace Chop.Api.Auth;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IAuditLogService _auditLogService;

    public AuthController(
        AppDbContext dbContext,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IRefreshTokenService refreshTokenService,
        IAuditLogService auditLogService)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _refreshTokenService = refreshTokenService;
        _auditLogService = auditLogService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
    {
        var login = request.Login.Trim();
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(request.Password))
        {
            await _auditLogService.WriteAsync(
                "auth.login.failed",
                "auth",
                null,
                null,
                null,
                """{"reason":"empty_credentials"}""",
                cancellationToken);
            return Unauthorized();
        }

        var user = await _dbContext.Users
            .Include(x => x.Credential)
            .Include(x => x.Roles)
            .SingleOrDefaultAsync(x => x.Login == login, cancellationToken);

        if (user is null || !user.IsActive || user.Credential is null)
        {
            await _auditLogService.WriteAsync(
                "auth.login.failed",
                "auth",
                null,
                null,
                null,
                """{"reason":"user_not_found_or_inactive"}""",
                cancellationToken);
            return Unauthorized();
        }

        if (!_passwordHasher.Verify(request.Password, user.Credential.PasswordHash))
        {
            await _auditLogService.WriteAsync(
                "auth.login.failed",
                "auth",
                null,
                null,
                null,
                """{"reason":"invalid_password"}""",
                cancellationToken);
            return Unauthorized();
        }

        var roles = user.Roles
            .Select(x => x.Role)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (roles.Length == 0)
        {
            await _auditLogService.WriteAsync(
                "auth.login.failed",
                "auth",
                user.Id,
                user.Id.ToString("D"),
                null,
                """{"reason":"user_without_roles"}""",
                cancellationToken);
            return Unauthorized();
        }

        var userId = user.Id.ToString("D");
        var (accessToken, expiresInSeconds) = _jwtTokenService.CreateAccessToken(userId, roles);
        var (refreshToken, _) = await _refreshTokenService.IssueAsync(userId, roles, cancellationToken);
        await _auditLogService.WriteAsync(
            "auth.login.success",
            "user",
            user.Id,
            userId,
            roles.FirstOrDefault(),
            """{"grant":"access+refresh"}""",
            cancellationToken);

        return Ok(new LoginResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresInSeconds = expiresInSeconds,
            User = new AuthUserDto
            {
                Id = userId,
                Roles = roles,
            },
        });
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<RefreshResponseDto>> Refresh([FromBody] RefreshRequestDto request, CancellationToken cancellationToken)
    {
        var rotated = await _refreshTokenService.RotateAsync(request.RefreshToken, cancellationToken);
        if (rotated.Outcome != RefreshRotateOutcome.Success || rotated.NewRecord is null)
        {
            var reason = rotated.Outcome == RefreshRotateOutcome.ReuseDetected ? "reuse_detected" : "invalid_refresh";
            await _auditLogService.WriteAsync(
                "auth.refresh.failed",
                "auth",
                null,
                null,
                null,
                $$"""{"reason":"{{reason}}"}""",
                cancellationToken);
            return Unauthorized();
        }

        var (accessToken, expiresInSeconds) = _jwtTokenService.CreateAccessToken(rotated.NewRecord.UserId, rotated.Roles);
        await _auditLogService.WriteAsync(
            "auth.refresh.success",
            "user",
            Guid.TryParse(rotated.NewRecord.UserId, out var parsedUserId) ? parsedUserId : null,
            rotated.NewRecord.UserId,
            rotated.Roles.FirstOrDefault(),
            """{"grant":"access+refresh"}""",
            cancellationToken);
        return Ok(new RefreshResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = rotated.RefreshToken,
            ExpiresInSeconds = expiresInSeconds,
        });
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequestDto request, CancellationToken cancellationToken)
    {
        var revoked = await _refreshTokenService.RevokeAsync(request.RefreshToken, cancellationToken);
        await _auditLogService.WriteAsync(
            revoked ? "auth.logout.success" : "auth.logout.noop",
            "auth",
            null,
            null,
            null,
            """{"operation":"refresh_revoke"}""",
            cancellationToken);
        return NoContent();
    }

    [HttpPost("invitations/accept")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<LoginResponseDto>> AcceptInvitation(
        [FromBody] AcceptInvitationRequestDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.InvitationToken) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            await _auditLogService.WriteAsync(
                "auth.invitation.accept.failed",
                "invitation",
                null,
                null,
                null,
                """{"reason":"empty_payload"}""",
                cancellationToken);
            return BadRequest("invitationToken and newPassword are required.");
        }

        var tokenHash = ComputeSha256(request.InvitationToken.Trim());
        var invitation = await _dbContext.Invitations
            .Include(x => x.User)
                .ThenInclude(x => x!.Credential)
            .Include(x => x.User)
                .ThenInclude(x => x!.Roles)
            .SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

        if (invitation is null || invitation.UsedAtUtc.HasValue || invitation.ExpiresAtUtc <= DateTime.UtcNow)
        {
            await _auditLogService.WriteAsync(
                "auth.invitation.accept.failed",
                "invitation",
                invitation?.Id,
                null,
                null,
                """{"reason":"invalid_or_expired"}""",
                cancellationToken);
            return Unauthorized();
        }

        var user = invitation.User;
        if (user is null || !user.IsActive)
        {
            await _auditLogService.WriteAsync(
                "auth.invitation.accept.failed",
                "invitation",
                invitation.Id,
                null,
                null,
                """{"reason":"user_inactive_or_missing"}""",
                cancellationToken);
            return Unauthorized();
        }

        var now = DateTime.UtcNow;
        if (user.Credential is null)
        {
            _dbContext.UserCredentials.Add(new UserCredential
            {
                UserId = user.Id,
                PasswordAlgo = "PBKDF2-SHA256",
                PasswordHash = _passwordHasher.Hash(request.NewPassword),
                PasswordChangedAtUtc = now,
            });
        }
        else
        {
            user.Credential.PasswordAlgo = "PBKDF2-SHA256";
            user.Credential.PasswordHash = _passwordHasher.Hash(request.NewPassword);
            user.Credential.PasswordChangedAtUtc = now;
        }

        invitation.UsedAtUtc = now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var roles = user.Roles
            .Select(x => x.Role)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (roles.Length == 0)
        {
            await _auditLogService.WriteAsync(
                "auth.invitation.accept.failed",
                "invitation",
                invitation.Id,
                user.Id.ToString("D"),
                null,
                """{"reason":"user_without_roles"}""",
                cancellationToken);
            return Unauthorized();
        }

        var userId = user.Id.ToString("D");
        var (accessToken, expiresInSeconds) = _jwtTokenService.CreateAccessToken(userId, roles);
        var (refreshToken, _) = await _refreshTokenService.IssueAsync(userId, roles, cancellationToken);

        await _auditLogService.WriteAsync(
            "auth.invitation.accept.success",
            "invitation",
            invitation.Id,
            userId,
            roles.FirstOrDefault(),
            """{"grant":"access+refresh"}""",
            cancellationToken);

        return Ok(new LoginResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresInSeconds = expiresInSeconds,
            User = new AuthUserDto
            {
                Id = userId,
                Roles = roles,
            },
        });
    }

    private static string ComputeSha256(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
