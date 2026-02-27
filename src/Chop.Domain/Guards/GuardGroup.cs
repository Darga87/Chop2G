namespace Chop.Domain.Guards;

public sealed class GuardGroup
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public ICollection<GuardGroupMember> Members { get; set; } = [];
}

