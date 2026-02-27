namespace Chop.Domain.Incidents;

public sealed class IncidentIdempotency
{
    public Guid Id { get; set; }

    public string ClientUserId { get; set; } = string.Empty;

    public string IdempotencyKey { get; set; } = string.Empty;

    public string RequestHash { get; set; } = string.Empty;

    public Guid IncidentId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }
}
