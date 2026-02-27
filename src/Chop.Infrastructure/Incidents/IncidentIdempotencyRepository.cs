using Chop.Application.Incidents;
using Chop.Domain.Incidents;
using Chop.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Chop.Infrastructure.Incidents;

public sealed class IncidentIdempotencyRepository : IIncidentIdempotencyRepository
{
    private readonly AppDbContext _dbContext;

    public IncidentIdempotencyRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<IncidentIdempotency?> FindAsync(string clientUserId, string idempotencyKey, CancellationToken cancellationToken) =>
        _dbContext.IncidentIdempotencies
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.ClientUserId == clientUserId && x.IdempotencyKey == idempotencyKey,
                cancellationToken);

    public Task AddAsync(IncidentIdempotency record, CancellationToken cancellationToken) =>
        _dbContext.IncidentIdempotencies.AddAsync(record, cancellationToken).AsTask();

    public Task<int> DeleteExpiredAsync(DateTime utcNow, CancellationToken cancellationToken) =>
        _dbContext.IncidentIdempotencies
            .Where(x => x.ExpiresAtUtc <= utcNow)
            .ExecuteDeleteAsync(cancellationToken);
}
