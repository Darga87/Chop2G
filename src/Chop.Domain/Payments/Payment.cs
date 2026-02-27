namespace Chop.Domain.Payments;

public sealed class Payment
{
    public Guid Id { get; set; }
    public string ClientUserId { get; set; } = string.Empty;
    public Guid? ImportId { get; set; }
    public Guid? ImportRowId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaidAtUtc { get; set; }
    public string Source { get; set; } = "BANK_IMPORT";
    public string? ExternalReference { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

