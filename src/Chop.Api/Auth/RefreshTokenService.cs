using System.Security.Cryptography;
using System.Text;
using Chop.Domain.Auth;
using Chop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Chop.Api.Auth;

public interface IRefreshTokenService
{
    Task<(string RefreshToken, RefreshToken Record)> IssueAsync(
        string userId,
        IEnumerable<string> roles,
        CancellationToken cancellationToken);

    Task<RefreshRotateResult> RotateAsync(
        string refreshToken,
        CancellationToken cancellationToken);

    Task<bool> RevokeAsync(string refreshToken, CancellationToken cancellationToken);
}

public enum RefreshRotateOutcome
{
    Success = 0,
    Invalid = 1,
    ReuseDetected = 2,
}

public sealed class RefreshRotateResult
{
    public RefreshRotateOutcome Outcome { get; private init; }

    public string RefreshToken { get; private init; } = string.Empty;

    public RefreshToken? NewRecord { get; private init; }

    public string[] Roles { get; private init; } = [];

    public static RefreshRotateResult Success(string refreshToken, RefreshToken newRecord, string[] roles) =>
        new()
        {
            Outcome = RefreshRotateOutcome.Success,
            RefreshToken = refreshToken,
            NewRecord = newRecord,
            Roles = roles,
        };

    public static RefreshRotateResult Invalid() =>
        new()
        {
            Outcome = RefreshRotateOutcome.Invalid,
        };

    public static RefreshRotateResult ReuseDetected() =>
        new()
        {
            Outcome = RefreshRotateOutcome.ReuseDetected,
        };
}

public sealed class RefreshTokenService : IRefreshTokenService
{
    private readonly AppDbContext _dbContext;
    private readonly JwtOptions _options;

    public RefreshTokenService(AppDbContext dbContext, IOptions<JwtOptions> options)
    {
        _dbContext = dbContext;
        _options = options.Value;
    }

    public async Task<(string RefreshToken, RefreshToken Record)> IssueAsync(
        string userId,
        IEnumerable<string> roles,
        CancellationToken cancellationToken)
    {
        var plainToken = GenerateToken();
        var tokenHash = ComputeHash(plainToken);
        var now = DateTime.UtcNow;

        var record = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RolesCsv = string.Join(',', roles.Distinct(StringComparer.OrdinalIgnoreCase)),
            TokenHash = tokenHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(_options.RefreshTokenTtlDays),
        };

        _dbContext.RefreshTokens.Add(record);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return (plainToken, record);
    }

    public async Task<RefreshRotateResult> RotateAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        var tokenHash = ComputeHash(refreshToken);
        var current = await _dbContext.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

        if (current is null || current.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return RefreshRotateResult.Invalid();
        }

        if (current.RevokedAtUtc.HasValue)
        {
            if (!string.IsNullOrWhiteSpace(current.ReplacedByTokenHash))
            {
                await RevokeAllActiveForUserAsync(current.UserId, cancellationToken);
                return RefreshRotateResult.ReuseDetected();
            }

            return RefreshRotateResult.Invalid();
        }

        var roles = SplitRoles(current.RolesCsv);
        var newToken = GenerateToken();
        var newHash = ComputeHash(newToken);
        var now = DateTime.UtcNow;

        current.RevokedAtUtc = now;
        current.ReplacedByTokenHash = newHash;

        var newRecord = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = current.UserId,
            RolesCsv = current.RolesCsv,
            TokenHash = newHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(_options.RefreshTokenTtlDays),
        };

        _dbContext.RefreshTokens.Add(newRecord);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return RefreshRotateResult.Success(newToken, newRecord, roles);
    }

    public async Task<bool> RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var tokenHash = ComputeHash(refreshToken);
        var current = await _dbContext.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
        if (current is null || current.RevokedAtUtc.HasValue)
        {
            return false;
        }

        current.RevokedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string GenerateToken()
    {
        var buffer = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(buffer);
    }

    private static string ComputeHash(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static string[] SplitRoles(string rolesCsv) =>
        rolesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private async Task RevokeAllActiveForUserAsync(string userId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var activeTokens = await _dbContext.RefreshTokens
            .Where(x => x.UserId == userId)
            .Where(x => !x.RevokedAtUtc.HasValue)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.RevokedAtUtc = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
