namespace Chop.Domain.Payments;

public sealed class BankImport
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileHash { get; set; } = string.Empty;
    public string Status { get; set; } = "DRAFT";
    public int TotalRows { get; set; }
    public int MatchedRows { get; set; }
    public int AmbiguousRows { get; set; }
    public int InvalidRows { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? AppliedByUserId { get; set; }
    public DateTime? AppliedAtUtc { get; set; }
    public ICollection<BankImportRow> Rows { get; set; } = new List<BankImportRow>();
}

