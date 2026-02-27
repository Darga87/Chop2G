namespace Chop.Application.Incidents;

public sealed class IdempotencyPersistenceRaceException : Exception
{
    public IdempotencyPersistenceRaceException()
        : base("Idempotency record already exists.")
    {
    }
}
