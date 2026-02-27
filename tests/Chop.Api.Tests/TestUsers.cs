namespace Chop.Api.Tests;

internal static class TestUsers
{
    internal static readonly TestUser Client = new(
        Guid.Parse("00000000-0000-0000-0000-000000000101"),
        "client",
        "client-pass",
        ["CLIENT"]);

    internal static readonly TestUser Operator = new(
        Guid.Parse("00000000-0000-0000-0000-000000000102"),
        "operator",
        "operator-pass",
        ["OPERATOR"]);

    internal static readonly TestUser Admin = new(
        Guid.Parse("00000000-0000-0000-0000-000000000103"),
        "admin",
        "admin-pass",
        ["ADMIN"]);

    internal static readonly TestUser SuperAdmin = new(
        Guid.Parse("00000000-0000-0000-0000-000000000104"),
        "superadmin",
        "superadmin-pass",
        ["SUPERADMIN"]);

    internal static readonly TestUser ClientDedupKey = new(
        Guid.Parse("00000000-0000-0000-0000-000000000105"),
        "client-dedup-key",
        "client-dedup-key-pass",
        ["CLIENT"]);

    internal static readonly TestUser ClientDedupWindow = new(
        Guid.Parse("00000000-0000-0000-0000-000000000106"),
        "client-dedup-window",
        "client-dedup-window-pass",
        ["CLIENT"]);

    internal static readonly TestUser ClientIdemConflict = new(
        Guid.Parse("00000000-0000-0000-0000-000000000107"),
        "client-idem-conflict",
        "client-idem-conflict-pass",
        ["CLIENT"]);

    internal static readonly TestUser ClientConcurrentNoKey = new(
        Guid.Parse("00000000-0000-0000-0000-000000000108"),
        "client-concurrent-no-key",
        "client-concurrent-no-key-pass",
        ["CLIENT"]);

    internal static readonly TestUser Guard = new(
        Guid.Parse("00000000-0000-0000-0000-000000000109"),
        "guard",
        "guard-pass",
        ["GUARD"]);

    internal static readonly TestUser GuardSecond = new(
        Guid.Parse("00000000-0000-0000-0000-000000000111"),
        "guard-second",
        "guard-second-pass",
        ["GUARD"]);

    internal static readonly TestUser Hr = new(
        Guid.Parse("00000000-0000-0000-0000-000000000112"),
        "hr",
        "hr-pass",
        ["HR"]);

    internal static readonly TestUser Manager = new(
        Guid.Parse("00000000-0000-0000-0000-000000000113"),
        "manager",
        "manager-pass",
        ["MANAGER"]);

    internal static readonly TestUser Accountant = new(
        Guid.Parse("00000000-0000-0000-0000-000000000114"),
        "accountant",
        "accountant-pass",
        ["ACCOUNTANT"]);

    internal static readonly TestUser ClientDetailsShape = new(
        Guid.Parse("00000000-0000-0000-0000-000000000110"),
        "client-details-shape",
        "client-details-shape-pass",
        ["CLIENT"]);

    internal static IEnumerable<TestUser> All()
    {
        yield return Client;
        yield return Operator;
        yield return Admin;
        yield return SuperAdmin;
        yield return ClientDedupKey;
        yield return ClientDedupWindow;
        yield return ClientIdemConflict;
        yield return ClientConcurrentNoKey;
        yield return Guard;
        yield return GuardSecond;
        yield return Hr;
        yield return Manager;
        yield return Accountant;
        yield return ClientDetailsShape;
    }
}

internal sealed record TestUser(Guid Id, string Login, string Password, string[] Roles)
{
    public string UserId => Id.ToString("D");
}
