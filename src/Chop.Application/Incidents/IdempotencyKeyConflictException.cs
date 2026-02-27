namespace Chop.Application.Incidents;

public sealed class IdempotencyKeyConflictException : Exception
{
    public IdempotencyKeyConflictException()
        : base("Idempotency-Key was already used with a different request payload.")
    {
    }
}
