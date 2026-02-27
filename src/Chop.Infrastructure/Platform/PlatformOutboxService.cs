using Chop.Application.Platform;
using Chop.Domain.Platform;
using Chop.Infrastructure.Persistence;

namespace Chop.Infrastructure.Platform;

public sealed class PlatformOutboxService : IPlatformOutboxService
{
    private readonly AppDbContext _dbContext;

    public PlatformOutboxService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task EnqueueAsync(
        string aggregateType,
        Guid aggregateId,
        string eventType,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        _dbContext.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            EventType = eventType,
            PayloadJson = payloadJson,
            Status = OutboxMessageStatus.Pending,
            AttemptCount = 0,
            NextAttemptAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
