using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Chop.Api.Backoffice.Payments;

public sealed class Payment1CParser
{
    private static readonly Encoding Utf8Strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly Encoding Windows1251;

    static Payment1CParser()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Windows1251 = Encoding.GetEncoding(1251);
    }

    public Payment1CParseResult Parse(byte[] payload)
    {
        if (payload.Length == 0)
        {
            throw new Payment1CParseException("Empty file.");
        }

        var content = Decode(payload);
        var lines = content
            .Split(["\r\n", "\n", "\r"], StringSplitOptions.None)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        if (lines.Length == 0)
        {
            throw new Payment1CParseException("Empty file.");
        }

        if (!lines[0].StartsWith("1CClientBankExchange", StringComparison.OrdinalIgnoreCase))
        {
            throw new Payment1CParseException("Unsupported format. 1CClientBankExchange is required.");
        }

        var rows = new List<Payment1CDocument>(capacity: 32);
        Payment1CDocumentBuilder? current = null;

        foreach (var line in lines)
        {
            var separatorIdx = line.IndexOf('=');
            if (separatorIdx <= 0)
            {
                continue;
            }

            var key = line[..separatorIdx].Trim();
            var value = line[(separatorIdx + 1)..].Trim();

            if (key.Equals("СекцияДокумент", StringComparison.OrdinalIgnoreCase))
            {
                current = new Payment1CDocumentBuilder(value);
                continue;
            }

            if (key.Equals("КонецДокумента", StringComparison.OrdinalIgnoreCase))
            {
                if (current is not null)
                {
                    rows.Add(current.Build());
                    current = null;
                }

                continue;
            }

            current?.Add(key, value);
        }

        if (current is not null)
        {
            // Tolerant mode: finalize even if `КонецДокумента` is missing.
            rows.Add(current.Build());
        }

        return new Payment1CParseResult(rows);
    }

    private static string Decode(byte[] payload)
    {
        try
        {
            return Utf8Strict.GetString(payload);
        }
        catch (DecoderFallbackException)
        {
            return Windows1251.GetString(payload);
        }
    }

    private sealed class Payment1CDocumentBuilder
    {
        private readonly Dictionary<string, List<string>> _allFields = new(StringComparer.OrdinalIgnoreCase);

        public Payment1CDocumentBuilder(string docType)
        {
            DocType = docType;
        }

        public string DocType { get; }

        public void Add(string key, string value)
        {
            if (!_allFields.TryGetValue(key, out var list))
            {
                list = [];
                _allFields[key] = list;
            }

            list.Add(value);
        }

        public Payment1CDocument Build()
        {
            var normalized = new Payment1CNormalizedFields
            {
                DocType = DocType,
                DocNo = Last("Номер"),
                DocDate = ParseDate(Last("Дата")),
                Amount = ParseDecimal(Last("Сумма")),
                PayerName = Last("Плательщик"),
                PayerInn = Last("ПлательщикИНН"),
                PayerAccount = Last("ПлательщикСчет"),
                ReceiverAccount = Last("ПолучательСчет"),
                Purpose = Last("НазначениеПлатежа"),
            };

            return new Payment1CDocument(normalized, _allFields.ToDictionary(x => x.Key, x => (IReadOnlyList<string>)x.Value.ToArray(), StringComparer.OrdinalIgnoreCase));
        }

        private string? Last(string key)
            => _allFields.TryGetValue(key, out var values) && values.Count > 0
                ? values[^1]
                : null;

        private static DateTime? ParseDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (DateTime.TryParseExact(value, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date))
            {
                return DateTime.SpecifyKind(date, DateTimeKind.Local).ToUniversalTime();
            }

            return null;
        }

        private static decimal? ParseDecimal(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var normalized = value.Replace(',', '.');
            return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
                ? amount
                : null;
        }
    }
}

public sealed record Payment1CParseResult(IReadOnlyList<Payment1CDocument> Documents);

public sealed record Payment1CDocument(
    Payment1CNormalizedFields Fields,
    IReadOnlyDictionary<string, IReadOnlyList<string>> Extra);

public sealed class Payment1CNormalizedFields
{
    public string DocType { get; init; } = string.Empty;
    public string? DocNo { get; init; }
    public DateTime? DocDate { get; init; }
    public decimal? Amount { get; init; }
    public string? PayerName { get; init; }
    public string? PayerInn { get; init; }
    public string? PayerAccount { get; init; }
    public string? ReceiverAccount { get; init; }
    public string? Purpose { get; init; }
}

public sealed class Payment1CParseException : Exception
{
    public Payment1CParseException(string message)
        : base(message)
    {
    }
}

public static partial class PaymentPurposeMatchers
{
    [GeneratedRegex(@"ID:\s*([A-Za-z0-9_-]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IdRegex();

    [GeneratedRegex(@"ДОГОВОР\s*№\s*([A-Za-z0-9_-]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ContractRegex();

    [GeneratedRegex(@"СЧЕТ\s*№\s*([A-Za-z0-9_-]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InvoiceRegex();

    public static IReadOnlyCollection<string> ExtractClientKeys(string? purpose)
    {
        if (string.IsNullOrWhiteSpace(purpose))
        {
            return [];
        }

        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in IdRegex().Matches(purpose))
        {
            if (match.Groups.Count > 1 && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
            {
                keys.Add(match.Groups[1].Value.Trim());
            }
        }

        foreach (Match match in ContractRegex().Matches(purpose))
        {
            if (match.Groups.Count > 1 && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
            {
                keys.Add(match.Groups[1].Value.Trim());
            }
        }

        foreach (Match match in InvoiceRegex().Matches(purpose))
        {
            if (match.Groups.Count > 1 && !string.IsNullOrWhiteSpace(match.Groups[1].Value))
            {
                keys.Add(match.Groups[1].Value.Trim());
            }
        }

        return keys.ToArray();
    }
}
