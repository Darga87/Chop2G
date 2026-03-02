namespace Chop.Api.Platform;

public interface IRealtimeBusPublisher
{
    bool IsEnabled { get; }

    Task PublishAsync(string eventType, string payloadJson, CancellationToken cancellationToken);
}
