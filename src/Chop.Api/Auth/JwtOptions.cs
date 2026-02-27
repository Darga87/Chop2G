namespace Chop.Api.Auth;

public sealed class JwtOptions
{
    public const string DefaultSigningKey = "dev-signing-key-change-me-please";
    public const string DevelopmentSigningKey = "dev-only-signing-key-at-least-32chars";
    public const string PlaceholderProductionSigningKey = "replace-in-production-with-secret-key-32+";

    public string Issuer { get; set; } = "Chop2G";

    public string Audience { get; set; } = "Chop2G.Api";

    public string SigningKey { get; set; } = DefaultSigningKey;

    public int AccessTokenTtlMinutes { get; set; } = 15;

    public int RefreshTokenTtlDays { get; set; } = 30;

    public static bool IsUnsafeSigningKey(string? signingKey)
    {
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            return true;
        }

        var key = signingKey.Trim();
        if (key.Length < 32)
        {
            return true;
        }

        return key.Equals(DefaultSigningKey, StringComparison.Ordinal)
            || key.Equals(DevelopmentSigningKey, StringComparison.Ordinal)
            || key.Equals(PlaceholderProductionSigningKey, StringComparison.Ordinal);
    }
}
