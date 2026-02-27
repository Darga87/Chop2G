using Chop.Application.Incidents;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Data;

namespace Chop.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private const int MaxSerializableRetries = 3;
    private readonly AppDbContext _dbContext;

    public UnitOfWork(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        SaveChangesInternalAsync(cancellationToken);

    public async Task<T> ExecuteSerializableAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxSerializableRetries; attempt++)
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            try
            {
                var result = await action(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (Exception ex) when (IsSerializationFailure(ex) && attempt < MaxSerializableRetries)
            {
                await transaction.RollbackAsync(cancellationToken);
                _dbContext.ChangeTracker.Clear();
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        throw new InvalidOperationException("Failed to execute serializable transaction after max retries.");
    }

    private async Task<int> SaveChangesInternalAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsIdempotencyUniqueViolation(ex))
        {
            throw new IdempotencyPersistenceRaceException();
        }
    }

    private static bool IsIdempotencyUniqueViolation(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("IX_incident_idempotency_ClientUserId_IdempotencyKey", StringComparison.OrdinalIgnoreCase)
               || message.Contains(
                   "UNIQUE constraint failed: incident_idempotency.ClientUserId, incident_idempotency.IdempotencyKey",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSerializationFailure(Exception ex) =>
        ex switch
        {
            PostgresException pg when pg.SqlState == "40001" => true,
            SqliteException sqlite when sqlite.SqliteErrorCode == 5 || sqlite.SqliteErrorCode == 6 => true,
            DbUpdateException dbUpdate when dbUpdate.InnerException is not null => IsSerializationFailure(dbUpdate.InnerException),
            _ => ex.InnerException is not null && IsSerializationFailure(ex.InnerException),
        };
}
