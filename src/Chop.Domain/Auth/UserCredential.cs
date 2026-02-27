namespace Chop.Domain.Auth;

public sealed class UserCredential
{
    public Guid UserId { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    public string PasswordAlgo { get; set; } = string.Empty;

    public DateTime PasswordChangedAtUtc { get; set; }

    public User? User { get; set; }
}
