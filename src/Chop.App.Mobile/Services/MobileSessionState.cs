using Chop.Shared.Contracts.Auth;

namespace Chop.App.Mobile.Services;

public sealed class MobileSessionState
{
    private const string AccessTokenKey = "auth.access_token";
    private const string RefreshTokenKey = "auth.refresh_token";
    private const string UserIdKey = "auth.user_id";
    private const string RolesKey = "auth.roles";
    private const string ApiBaseUrlKey = "app.api_base_url";

    private bool _initialized;

    public string AccessToken { get; private set; } = string.Empty;

    public string RefreshToken { get; private set; } = string.Empty;

    public string UserId { get; private set; } = string.Empty;

    public string[] Roles { get; private set; } = [];

    public string ApiBaseUrl { get; private set; } = "http://10.0.2.2:5261";

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(AccessToken);

    public bool IsClient => Roles.Any(x => string.Equals(x, "CLIENT", StringComparison.OrdinalIgnoreCase));

    public event Action? Changed;

    public async Task EnsureInitializedAsync()
    {
        if (_initialized)
        {
            return;
        }

        ApiBaseUrl = Preferences.Default.Get(ApiBaseUrlKey, ApiBaseUrl);

        AccessToken = await ReadSecureAsync(AccessTokenKey);
        RefreshToken = await ReadSecureAsync(RefreshTokenKey);
        UserId = await ReadSecureAsync(UserIdKey);

        var rolesRaw = await ReadSecureAsync(RolesKey);
        Roles = string.IsNullOrWhiteSpace(rolesRaw)
            ? []
            : rolesRaw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        _initialized = true;
    }

    public void SetApiBaseUrl(string baseUrl)
    {
        ApiBaseUrl = NormalizeApiBaseUrl(baseUrl);
        Preferences.Default.Set(ApiBaseUrlKey, ApiBaseUrl);
        Changed?.Invoke();
    }

    public async Task SignInAsync(LoginResponseDto response)
    {
        AccessToken = response.AccessToken;
        RefreshToken = response.RefreshToken;
        UserId = response.User.Id;
        Roles = response.User.Roles;

        await WriteSecureAsync(AccessTokenKey, AccessToken);
        await WriteSecureAsync(RefreshTokenKey, RefreshToken);
        await WriteSecureAsync(UserIdKey, UserId);
        await WriteSecureAsync(RolesKey, string.Join(",", Roles));

        Changed?.Invoke();
    }

    public async Task SignOutAsync()
    {
        AccessToken = string.Empty;
        RefreshToken = string.Empty;
        UserId = string.Empty;
        Roles = [];

        SecureStorage.Default.Remove(AccessTokenKey);
        SecureStorage.Default.Remove(RefreshTokenKey);
        SecureStorage.Default.Remove(UserIdKey);
        SecureStorage.Default.Remove(RolesKey);

        await Task.CompletedTask;
        Changed?.Invoke();
    }

    private static async Task<string> ReadSecureAsync(string key)
    {
        try
        {
            return await SecureStorage.Default.GetAsync(key) ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static async Task WriteSecureAsync(string key, string value)
    {
        try
        {
            await SecureStorage.Default.SetAsync(key, value);
        }
        catch
        {
            // In emulator/device issues with secure storage we keep in-memory values.
        }
    }

    private static string NormalizeApiBaseUrl(string baseUrl)
    {
        var value = (baseUrl ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return "http://10.0.2.2:5261";
        }

        if (!value.Contains("://", StringComparison.Ordinal))
        {
            value = $"http://{value}";
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return value.TrimEnd('/');
        }

        return uri.ToString().TrimEnd('/');
    }
}
