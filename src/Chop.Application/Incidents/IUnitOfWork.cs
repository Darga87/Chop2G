namespace Chop.Application.Incidents;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);

    Task<T> ExecuteSerializableAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken);
}
