namespace Chop.Domain.Clients;

public sealed class ClientPhone
{
    public Guid Id { get; set; }

    public Guid ClientProfileId { get; set; }

    public ClientProfile ClientProfile { get; set; } = null!;

    public string Phone { get; set; } = string.Empty;

    public string Type { get; set; } = "PRIMARY";

    public bool IsPrimary { get; set; }
}
