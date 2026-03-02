using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Chop.Shared.Contracts.Realtime;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Chop.Api.Auth;

public interface IJwtTokenService
{
    (string AccessToken, int ExpiresInSeconds) CreateAccessToken(string userId, IEnumerable<string> roles);
}

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;
    private readonly SigningCredentials _signingCredentials;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public (string AccessToken, int ExpiresInSeconds) CreateAccessToken(string userId, IEnumerable<string> roles)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_options.AccessTokenTtlMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(ClaimTypes.NameIdentifier, userId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };
        var distinctRoles = roles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        claims.AddRange(distinctRoles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(BuildScopeClaims(distinctRoles));

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: _signingCredentials);

        var tokenValue = new JwtSecurityTokenHandler().WriteToken(token);
        var expiresInSeconds = (int)(expires - now).TotalSeconds;
        return (tokenValue, expiresInSeconds);
    }

    private static IEnumerable<Claim> BuildScopeClaims(IReadOnlyCollection<string> roles)
    {
        var isOpsUser = roles.Contains("OPERATOR", StringComparer.OrdinalIgnoreCase)
            || roles.Contains("ADMIN", StringComparer.OrdinalIgnoreCase)
            || roles.Contains("SUPERADMIN", StringComparer.OrdinalIgnoreCase);

        if (!isOpsUser)
        {
            return [];
        }

        return
        [
            new Claim(IncidentRealtimeGroups.ClientScopeClaim, IncidentRealtimeGroups.ScopeAll),
            new Claim(IncidentRealtimeGroups.RegionScopeClaim, IncidentRealtimeGroups.ScopeAll),
            new Claim(IncidentRealtimeGroups.ShiftScopeClaim, IncidentRealtimeGroups.ScopeAll),
        ];
    }
}
