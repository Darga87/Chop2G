namespace Chop.Domain.Clients;

public sealed class BillingTariff
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal MonthlyFee { get; set; }
    public string Currency { get; set; } = "KZT";
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
