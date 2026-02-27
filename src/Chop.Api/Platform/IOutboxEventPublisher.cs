namespace Chop.Api.Platform;

public interface IOutboxEventPublisher
{
    Task PublishAsync(string eventType, string payloadJson, CancellationToken cancellationToken);
}
