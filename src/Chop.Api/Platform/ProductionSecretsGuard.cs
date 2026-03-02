namespace Chop.Api.Platform;

public static class ProductionSecretsGuard
{
    public static void EnsureConfigured(IConfiguration configuration, IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            return;
        }

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (IsMissingOrPlaceholder(connectionString))
        {
            throw new InvalidOperationException(
                "DefaultConnection is not configured for non-Development environment. Use secret manager/env var.");
        }

        var jwtSigningKey = configuration["Jwt:SigningKey"];
        if (IsMissingOrPlaceholder(jwtSigningKey))
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey is not configured for non-Development environment. Use secret manager/env var.");
        }
    }

    private static bool IsMissingOrPlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var trimmed = value.Trim();
        return trimmed.Contains("replace", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("changeme", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("example", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("__", StringComparison.OrdinalIgnoreCase);
    }
}
