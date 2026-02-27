using System.Text;
using Chop.Api.Backoffice.Payments;

namespace Chop.Api.Tests;

public sealed class Payment1CParserTests
{
    static Payment1CParserTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private readonly Payment1CParser _parser = new();

    [Fact]
    public void Parse_ValidFile_ReturnsDocumentsWithNormalizedFields()
    {
        const string payload = """
1CClientBankExchange=1.03
СекцияДокумент=ПлатежноеПоручение
Номер=42
Дата=25.02.2026
Сумма=12500,50
Плательщик=Тестовый плательщик
ПлательщикИНН=123456789012
ПлательщикСчет=KZ123
ПолучательСчет=KZ999
НазначениеПлатежа=ID:00000000-0000-0000-0000-000000000101
КонецДокумента=
""";

        var result = _parser.Parse(Encoding.UTF8.GetBytes(payload));
        var doc = Assert.Single(result.Documents);
        Assert.Equal("ПлатежноеПоручение", doc.Fields.DocType);
        Assert.Equal("42", doc.Fields.DocNo);
        Assert.Equal(12500.50m, doc.Fields.Amount);
        Assert.NotNull(doc.Fields.DocDate);
        Assert.Equal("Тестовый плательщик", doc.Fields.PayerName);
    }

    [Fact]
    public void Parse_Empty_Throws()
    {
        var ex = Assert.Throws<Payment1CParseException>(() => _parser.Parse([]));
        Assert.Contains("Empty", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_BrokenDelimiter_IsTolerantAndDoesNotCrash()
    {
        const string payload = """
1CClientBankExchange=1.03
СекцияДокумент=ПлатежноеПоручение
Номер:42
Дата=25.02.2026
Сумма=1000
КонецДокумента=
""";

        var result = _parser.Parse(Encoding.UTF8.GetBytes(payload));
        var doc = Assert.Single(result.Documents);
        Assert.Null(doc.Fields.DocNo);
        Assert.Equal(1000m, doc.Fields.Amount);
    }

    [Fact]
    public void Parse_Cp1251Payload_IsDecoded()
    {
        const string payload = """
1CClientBankExchange=1.03
СекцияДокумент=ПлатежноеПоручение
Номер=77
Дата=25.02.2026
Сумма=5000
НазначениеПлатежа=ДОГОВОР №ABC-77
КонецДокумента=
""";

        var cp1251 = Encoding.GetEncoding(1251).GetBytes(payload);
        var result = _parser.Parse(cp1251);
        var doc = Assert.Single(result.Documents);
        Assert.Equal("77", doc.Fields.DocNo);
        Assert.Contains("ДОГОВОР", doc.Fields.Purpose, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_DuplicateKeys_StoresAllValuesAndUsesLastForNormalization()
    {
        const string payload = """
1CClientBankExchange=1.03
СекцияДокумент=ПлатежноеПоручение
Номер=1
Номер=2
Дата=25.02.2026
Сумма=100
КонецДокумента=
""";

        var result = _parser.Parse(Encoding.UTF8.GetBytes(payload));
        var doc = Assert.Single(result.Documents);
        Assert.Equal("2", doc.Fields.DocNo);
        Assert.True(doc.Extra.TryGetValue("Номер", out var values));
        Assert.Equal(2, values!.Count);
    }
}
