using Chop.Shared.Contracts.Guards;

namespace Chop.Application.Guards;

public interface IGuardLocationService
{
    Task<GuardLocationPingResult> PingAsync(string guardUserId, GuardLocationPingDto request, CancellationToken cancellationToken);
}

public sealed class GuardLocationPingResult
{
    public bool ShouldPublishRealtime { get; set; }
}
