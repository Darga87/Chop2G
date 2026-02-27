namespace Chop.Domain.Payments;

public sealed class BankImportRow
{
    public Guid Id { get; set; }
    public Guid ImportId { get; set; }
    public BankImport Import { get; set; } = null!;
    public string Reference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime PaymentDateUtc { get; set; }
    public string MatchStatus { get; set; } = "UNMATCHED";
    public string? ClientUserId { get; set; }
    public string? ClientDisplayName { get; set; }
    public string? CandidateClientIdsJson { get; set; }
    public string DocType { get; set; } = string.Empty;
    public string? DocNo { get; set; }
    public DateTime? DocDateUtc { get; set; }
    public string? PayerName { get; set; }
    public string? PayerInn { get; set; }
    public string? PayerAccount { get; set; }
    public string? ReceiverAccount { get; set; }
    public string? Purpose { get; set; }
    public string ExtraJson { get; set; } = "{}";
}

