namespace Chop.Application.Platform;

public interface IPlatformOutboxService
{
    Task EnqueueAsync(
        string aggregateType,
        Guid aggregateId,
        string eventType,
        string payloadJson,
        CancellationToken cancellationToken);
}
