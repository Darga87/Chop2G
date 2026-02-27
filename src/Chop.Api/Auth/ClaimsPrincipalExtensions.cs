using System.Security.Claims;

namespace Chop.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    public static string? GetUserId(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier);

    public static string? GetHighestRole(this ClaimsPrincipal user)
    {
        if (user.IsInRole("SUPERADMIN"))
        {
            return "SUPERADMIN";
        }

        if (user.IsInRole("ADMIN"))
        {
            return "ADMIN";
        }

        if (user.IsInRole("OPERATOR"))
        {
            return "OPERATOR";
        }

        if (user.IsInRole("HR"))
        {
            return "HR";
        }

        if (user.IsInRole("MANAGER"))
        {
            return "MANAGER";
        }

        if (user.IsInRole("ACCOUNTANT"))
        {
            return "ACCOUNTANT";
        }

        if (user.IsInRole("GUARD"))
        {
            return "GUARD";
        }

        if (user.IsInRole("CLIENT"))
        {
            return "CLIENT";
        }

        return null;
    }
}
