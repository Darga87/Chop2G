namespace Chop.Web.Auth;

public static class RoleConstants
{
    public const string Hr = "HR";

    public const string Operator = "OPERATOR";

    public const string Admin = "ADMIN";

    public const string SuperAdmin = "SUPERADMIN";

    public const string Manager = "MANAGER";

    public const string Accountant = "ACCOUNTANT";

    public static readonly string[] OpsRoles = [Operator, Admin, SuperAdmin];

    public static readonly string[] HrRoles = [Hr, Admin, SuperAdmin];

    public static readonly string[] AdminRoles = [Admin, SuperAdmin];

    public static readonly string[] SuperAdminOnlyRoles = [SuperAdmin];

    public static readonly string[] ManagerRoles = [Manager, Admin, SuperAdmin];

    public static readonly string[] AccountantRoles = [Accountant, Admin, SuperAdmin];
}
