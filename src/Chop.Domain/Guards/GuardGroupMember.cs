namespace Chop.Domain.Guards;

public sealed class GuardGroupMember
{
    public Guid Id { get; set; }

    public Guid GuardGroupId { get; set; }

    public GuardGroup? GuardGroup { get; set; }

    public string GuardUserId { get; set; } = string.Empty;

    public bool IsCommander { get; set; }

    public DateTime AddedAtUtc { get; set; }
}

