namespace Chop.Domain.Auth;

public sealed class UserRole
{
    public Guid UserId { get; set; }

    public string Role { get; set; } = string.Empty;

    public User? User { get; set; }
}
