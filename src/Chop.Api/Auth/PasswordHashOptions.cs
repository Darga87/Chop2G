namespace Chop.Api.Auth;

public sealed class PasswordHashOptions
{
    public int Iterations { get; set; } = 120_000;

    public int SaltSizeBytes { get; set; } = 16;

    public int KeySizeBytes { get; set; } = 32;
}
