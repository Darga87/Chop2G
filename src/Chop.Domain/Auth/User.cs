namespace Chop.Domain.Auth;

public sealed class User
{
    public Guid Id { get; set; }

    public string Login { get; set; } = string.Empty;

    public string? FullName { get; set; }

    public string? CallSign { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public UserCredential? Credential { get; set; }

    public ICollection<UserRole> Roles { get; set; } = [];
}
