namespace Chop.Domain.Platform;

public sealed class AuditLogEntry
{
    public Guid Id { get; set; }

    public string? ActorUserId { get; set; }

    public string? ActorRole { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public Guid? EntityId { get; set; }

    public string? ChangesJson { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
