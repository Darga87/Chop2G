namespace Chop.Domain.Clients;

public sealed class ClientProfile
{
    public Guid Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string Tariff { get; set; } = "STANDARD";

    public string BillingStatus { get; set; } = "ACTIVE";

    public DateTime LastPaymentAtUtc { get; set; }

    public bool HasDebt { get; set; }

    public ICollection<ClientPhone> Phones { get; set; } = new List<ClientPhone>();

    public ICollection<ClientAddress> Addresses { get; set; } = new List<ClientAddress>();
}
