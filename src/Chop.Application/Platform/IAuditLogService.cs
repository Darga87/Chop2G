namespace Chop.Application.Platform;

public interface IAuditLogService
{
    Task WriteAsync(
        string action,
        string entityType,
        Guid? entityId,
        string? actorUserId,
        string? actorRole,
        string? changesJson,
        CancellationToken cancellationToken);
}
