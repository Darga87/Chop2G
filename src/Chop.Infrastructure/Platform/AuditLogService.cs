using Chop.Application.Platform;
using Chop.Domain.Platform;
using Chop.Infrastructure.Persistence;

namespace Chop.Infrastructure.Platform;

public sealed class AuditLogService : IAuditLogService
{
    private readonly AppDbContext _dbContext;

    public AuditLogService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task WriteAsync(
        string action,
        string entityType,
        Guid? entityId,
        string? actorUserId,
        string? actorRole,
        string? changesJson,
        CancellationToken cancellationToken)
    {
        _dbContext.AuditLogEntries.Add(new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            ActorUserId = actorUserId,
            ActorRole = actorRole,
            ChangesJson = changesJson,
            CreatedAtUtc = DateTime.UtcNow,
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
