using Chop.Domain.Incidents;

namespace Chop.Application.Incidents;

public interface IIncidentIdempotencyRepository
{
    Task<IncidentIdempotency?> FindAsync(string clientUserId, string idempotencyKey, CancellationToken cancellationToken);

    Task AddAsync(IncidentIdempotency record, CancellationToken cancellationToken);

    Task<int> DeleteExpiredAsync(DateTime utcNow, CancellationToken cancellationToken);
}
