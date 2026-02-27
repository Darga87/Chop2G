namespace Chop.Web.Auth;

public static class AuthNavigation
{
    public static string ResolveDefaultRoute(WebAuthSession session)
    {
        if (session.IsInAnyRole(RoleConstants.SuperAdminOnlyRoles))
        {
            return "/superadmin/settings";
        }

        if (session.IsInAnyRole(RoleConstants.AdminRoles))
        {
            return "/admin/clients";
        }

        if (session.IsInAnyRole(RoleConstants.ManagerRoles))
        {
            return "/manager/clients";
        }

        if (session.IsInAnyRole(RoleConstants.AccountantRoles))
        {
            return "/accountant/payments/import";
        }

        if (session.IsInAnyRole(RoleConstants.HrRoles))
        {
            return "/hr/guards";
        }

        if (session.IsInAnyRole(RoleConstants.OpsRoles))
        {
            return "/operator/dashboard";
        }

        return "/forbidden";
    }
}
