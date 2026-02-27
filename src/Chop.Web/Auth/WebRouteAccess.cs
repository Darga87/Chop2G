namespace Chop.Web.Auth;

public static class WebRouteAccess
{
    public static RouteAccessPolicy Resolve(string relativePath)
    {
        var path = (relativePath ?? string.Empty).Trim().Trim('/').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(path))
        {
            return RouteAccessPolicy.Public();
        }

        if (path is "login" or "forbidden" or "error")
        {
            return RouteAccessPolicy.Public();
        }

        if (path.StartsWith("operator/"))
        {
            return RouteAccessPolicy.ForRoles(RoleConstants.OpsRoles);
        }

        if (path.StartsWith("hr/"))
        {
            return RouteAccessPolicy.ForRoles(RoleConstants.HrRoles);
        }

        if (path.StartsWith("admin/"))
        {
            return RouteAccessPolicy.ForRoles(RoleConstants.AdminRoles);
        }

        if (path.StartsWith("manager/"))
        {
            return RouteAccessPolicy.ForRoles(RoleConstants.ManagerRoles);
        }

        if (path.StartsWith("accountant/"))
        {
            return RouteAccessPolicy.ForRoles(RoleConstants.AccountantRoles);
        }

        if (path.StartsWith("superadmin/"))
        {
            return RouteAccessPolicy.ForRoles(RoleConstants.SuperAdminOnlyRoles);
        }

        return RouteAccessPolicy.Authenticated();
    }
}

public readonly record struct RouteAccessPolicy(bool RequiresAuthentication, IReadOnlyCollection<string> AllowedRoles)
{
    public static RouteAccessPolicy Public() => new(false, []);

    public static RouteAccessPolicy Authenticated() => new(true, []);

    public static RouteAccessPolicy ForRoles(params string[] roles) => new(true, roles);
}
