namespace Chop.Shared.Contracts.Auth;

public sealed class LoginRequestDto
{
    public string Login { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}

public sealed class RefreshRequestDto
{
    public string RefreshToken { get; set; } = string.Empty;
}

public sealed class AcceptInvitationRequestDto
{
    public string InvitationToken { get; set; } = string.Empty;

    public string NewPassword { get; set; } = string.Empty;
}

public sealed class LoginResponseDto
{
    public string AccessToken { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public int ExpiresInSeconds { get; set; }

    public AuthUserDto User { get; set; } = new();
}

public sealed class RefreshResponseDto
{
    public string AccessToken { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public int ExpiresInSeconds { get; set; }
}

public sealed class AuthUserDto
{
    public string Id { get; set; } = string.Empty;

    public string[] Roles { get; set; } = [];
}
