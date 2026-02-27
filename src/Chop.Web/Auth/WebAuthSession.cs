using Chop.Shared.Contracts.Auth;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace Chop.Web.Auth;

public sealed class WebAuthSession
{
    private const string AccessTokenCookie = "chop_access_token";
    private const string RefreshTokenCookie = "chop_refresh_token";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public event Action? Changed;

    public string? AccessToken { get; private set; }

    public string? RefreshToken { get; private set; }

    public string? UserId { get; private set; }

    public IReadOnlyCollection<string> Roles { get; private set; } = [];

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(AccessToken);

    public bool IsInRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);

    public bool IsInAnyRole(params string[] roles) => roles.Any(IsInRole);

    public WebAuthSession(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
        RestoreFromCookies();
    }

    public void SignIn(LoginResponseDto response)
    {
        AccessToken = response.AccessToken;
        RefreshToken = response.RefreshToken;
        UserId = response.User.Id;
        Roles = response.User.Roles;
        Changed?.Invoke();
    }

    public void SignInFromToken(string accessToken, string? refreshToken = null)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        Roles = ParseRolesFromJwt(accessToken);
        UserId = ParseUserIdFromJwt(accessToken);
        Changed?.Invoke();
    }

    public void SignOut()
    {
        AccessToken = null;
        RefreshToken = null;
        UserId = null;
        Roles = [];
        Changed?.Invoke();
    }

    private void RestoreFromCookies()
    {
        var requestCookies = _httpContextAccessor.HttpContext?.Request?.Cookies;
        if (requestCookies is null)
        {
            return;
        }

        if (!requestCookies.TryGetValue(AccessTokenCookie, out var accessToken) || string.IsNullOrWhiteSpace(accessToken))
        {
            return;
        }

        requestCookies.TryGetValue(RefreshTokenCookie, out var refreshToken);
        SignInFromToken(accessToken, refreshToken);
    }

    private static IReadOnlyCollection<string> ParseRolesFromJwt(string token)
    {
        try
        {
            var payload = ParsePayload(token);
            var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddRoleClaim(payload, "role", roles);
            AddRoleClaim(payload, "roles", roles);
            AddRoleClaim(payload, "http://schemas.microsoft.com/ws/2008/06/identity/claims/role", roles);

            return roles.ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static string? ParseUserIdFromJwt(string token)
    {
        try
        {
            var payload = ParsePayload(token);
            if (payload.TryGetProperty("sub", out var sub))
            {
                return sub.GetString();
            }

            if (payload.TryGetProperty("nameid", out var nameId))
            {
                return nameId.GetString();
            }

            if (payload.TryGetProperty("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", out var claim))
            {
                return claim.GetString();
            }
        }
        catch
        {
            // Ignore malformed token in UI session restore path.
        }

        return null;
    }

    private static JsonElement ParsePayload(string token)
    {
        var parts = token.Split('.');
        if (parts.Length < 2)
        {
            throw new InvalidOperationException("Invalid JWT format.");
        }

        var payload = parts[1]
            .Replace('-', '+')
            .Replace('_', '/');
        var padding = 4 - (payload.Length % 4);
        if (padding is > 0 and < 4)
        {
            payload = payload + new string('=', padding);
        }

        var bytes = Convert.FromBase64String(payload);
        using var doc = JsonDocument.Parse(bytes);
        return doc.RootElement.Clone();
    }

    private static void AddRoleClaim(JsonElement payload, string name, HashSet<string> roles)
    {
        if (!payload.TryGetProperty(name, out var value))
        {
            return;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            var role = value.GetString();
            if (!string.IsNullOrWhiteSpace(role))
            {
                roles.Add(role);
            }

            return;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var role = item.GetString();
            if (!string.IsNullOrWhiteSpace(role))
            {
                roles.Add(role);
            }
        }
    }
}
