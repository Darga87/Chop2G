namespace Chop.Domain.Guards;

public sealed class GuardShift
{
    public Guid Id { get; set; }

    public string GuardUserId { get; set; } = string.Empty;

    public Guid? GuardGroupId { get; set; }

    public GuardGroup? GuardGroup { get; set; }

    public Guid? SecurityPointId { get; set; }

    public SecurityPoint? SecurityPoint { get; set; }

    public DateTime StartedAtUtc { get; set; }

    public DateTime? EndedAtUtc { get; set; }
}

