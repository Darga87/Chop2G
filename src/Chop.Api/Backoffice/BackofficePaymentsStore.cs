using System.Security.Cryptography;
using System.Text.Json;
using Chop.Api.Backoffice.Payments;
using Chop.Application.Platform;
using Chop.Domain.Clients;
using Chop.Domain.Payments;
using Chop.Infrastructure.Persistence;
using Chop.Shared.Contracts.Backoffice;
using Microsoft.EntityFrameworkCore;

namespace Chop.Api.Backoffice;

public sealed class BackofficePaymentsStore
{
    private readonly AppDbContext _dbContext;
    private readonly IAuditLogService _auditLogService;
    private readonly Payment1CParser _parser = new();

    public BackofficePaymentsStore(AppDbContext dbContext, IAuditLogService auditLogService)
    {
        _dbContext = dbContext;
        _auditLogService = auditLogService;
    }

    public async Task<IReadOnlyCollection<PaymentImportItemDto>> GetImportsAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.BankImports
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new PaymentImportItemDto
            {
                Id = x.Id,
                FileName = x.FileName,
                Status = x.Status,
                TotalRows = x.TotalRows,
                MatchedRows = x.MatchedRows,
                AmbiguousRows = x.AmbiguousRows,
                InvalidRows = x.InvalidRows,
                CreatedAtUtc = x.CreatedAtUtc,
            })
            .ToArrayAsync(cancellationToken);
    }

    public async Task<PaymentImportItemDto?> GetImportAsync(Guid importId, CancellationToken cancellationToken)
    {
        return await _dbContext.BankImports
            .AsNoTracking()
            .Where(x => x.Id == importId)
            .Select(x => new PaymentImportItemDto
            {
                Id = x.Id,
                FileName = x.FileName,
                Status = x.Status,
                TotalRows = x.TotalRows,
                MatchedRows = x.MatchedRows,
                AmbiguousRows = x.AmbiguousRows,
                InvalidRows = x.InvalidRows,
                CreatedAtUtc = x.CreatedAtUtc,
            })
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<PaymentImportRowItemDto>> GetRowsAsync(Guid importId, CancellationToken cancellationToken)
    {
        return await _dbContext.BankImportRows
            .AsNoTracking()
            .Where(x => x.ImportId == importId)
            .OrderBy(x => x.PaymentDateUtc)
            .ThenBy(x => x.Reference)
            .Select(x => new PaymentImportRowItemDto
            {
                Id = x.Id,
                Reference = x.Reference,
                Amount = x.Amount,
                PaymentDateUtc = x.PaymentDateUtc,
                MatchStatus = x.MatchStatus,
                ClientDisplayName = x.ClientDisplayName,
            })
            .ToArrayAsync(cancellationToken);
    }

    public async Task<PaymentImportItemDto> CreateDraftAsync(
        string fileName,
        byte[] payload,
        string? actorUserId,
        string? actorRole,
        CancellationToken cancellationToken)
    {
        var parsed = _parser.Parse(payload);
        var fileHash = ComputeSha256(payload);

        var existing = await _dbContext.BankImports.AsNoTracking()
            .Where(x => x.FileHash == fileHash)
            .Select(x => new PaymentImportItemDto
            {
                Id = x.Id,
                FileName = x.FileName,
                Status = "DUPLICATE",
                TotalRows = x.TotalRows,
                MatchedRows = x.MatchedRows,
                AmbiguousRows = x.AmbiguousRows,
                InvalidRows = x.InvalidRows,
                CreatedAtUtc = x.CreatedAtUtc,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            await _auditLogService.WriteAsync(
                "payments.import.duplicate",
                "bank_import",
                existing.Id,
                actorUserId,
                actorRole,
                $$"""{"fileName":"{{fileName}}"}""",
                cancellationToken);
            return existing;
        }

        var clientLookup = await LoadClientLookupAsync(cancellationToken);
        var rowRecords = BuildRows(parsed.Documents, clientLookup);

        var import = new BankImport
        {
            Id = Guid.NewGuid(),
            FileName = fileName,
            FileHash = fileHash,
            Status = "DRAFT",
            TotalRows = rowRecords.Count,
            MatchedRows = rowRecords.Count(x => x.MatchStatus is "MATCHED" or "MANUAL"),
            AmbiguousRows = rowRecords.Count(x => x.MatchStatus == "AMBIGUOUS"),
            InvalidRows = rowRecords.Count(x => x.MatchStatus == "INVALID"),
            CreatedByUserId = actorUserId,
            CreatedAtUtc = DateTime.UtcNow,
            Rows = rowRecords,
        };

        _dbContext.BankImports.Add(import);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.WriteAsync(
            "payments.import.upload",
            "bank_import",
            import.Id,
            actorUserId,
            actorRole,
            $$"""{"rows":{{import.TotalRows}},"fileName":"{{fileName}}"}""",
            cancellationToken);

        return new PaymentImportItemDto
        {
            Id = import.Id,
            FileName = import.FileName,
            Status = import.Status,
            TotalRows = import.TotalRows,
            MatchedRows = import.MatchedRows,
            AmbiguousRows = import.AmbiguousRows,
            InvalidRows = import.InvalidRows,
            CreatedAtUtc = import.CreatedAtUtc,
        };
    }

    public async Task<bool> ApplyAsync(Guid importId, string? actorUserId, string? actorRole, CancellationToken cancellationToken)
    {
        var import = await _dbContext.BankImports
            .Include(x => x.Rows)
            .SingleOrDefaultAsync(x => x.Id == importId, cancellationToken);
        if (import is null)
        {
            return false;
        }

        if (import.Status == "APPLIED")
        {
            return true;
        }

        var applicableRows = import.Rows
            .Where(x => x.MatchStatus is "MATCHED" or "MANUAL")
            .Where(x => !string.IsNullOrWhiteSpace(x.ClientUserId))
            .ToArray();

        var rowIds = applicableRows.Select(x => x.Id).ToArray();
        var alreadyAppliedRowIds = await _dbContext.Payments
            .AsNoTracking()
            .Where(x => x.ImportRowId != null && rowIds.Contains(x.ImportRowId.Value))
            .Select(x => x.ImportRowId!.Value)
            .ToArrayAsync(cancellationToken);
        var alreadyAppliedSet = alreadyAppliedRowIds.ToHashSet();

        var now = DateTime.UtcNow;
        var newPayments = applicableRows
            .Where(x => !alreadyAppliedSet.Contains(x.Id))
            .Select(row => new Payment
            {
                Id = Guid.NewGuid(),
                ClientUserId = row.ClientUserId!,
                ImportId = import.Id,
                ImportRowId = row.Id,
                Amount = row.Amount,
                PaidAtUtc = row.PaymentDateUtc,
                Source = "BANK_IMPORT",
                ExternalReference = row.Reference,
                CreatedAtUtc = now,
            })
            .ToArray();

        if (newPayments.Length > 0)
        {
            _dbContext.Payments.AddRange(newPayments);

            var lastPaymentsByClient = newPayments
                .GroupBy(x => x.ClientUserId)
                .Select(g => new { ClientUserId = g.Key, LastPaidAt = g.Max(x => x.PaidAtUtc) })
                .ToArray();

            foreach (var item in lastPaymentsByClient)
            {
                var profile = await _dbContext.ClientProfiles
                    .SingleOrDefaultAsync(x => x.UserId == item.ClientUserId, cancellationToken);
                if (profile is null)
                {
                    continue;
                }

                if (profile.LastPaymentAtUtc < item.LastPaidAt)
                {
                    profile.LastPaymentAtUtc = item.LastPaidAt;
                }
            }
        }

        import.Status = "APPLIED";
        import.AppliedAtUtc = now;
        import.AppliedByUserId = actorUserId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.WriteAsync(
            "payments.import.apply",
            "bank_import",
            import.Id,
            actorUserId,
            actorRole,
            $$"""{"createdPayments":{{newPayments.Length}}}""",
            cancellationToken);

        return true;
    }

    public async Task<bool> ManualMatchAsync(
        Guid importId,
        Guid rowId,
        string? clientUserId,
        string? clientDisplayName,
        string? actorUserId,
        string? actorRole,
        CancellationToken cancellationToken)
    {
        var row = await _dbContext.BankImportRows
            .SingleOrDefaultAsync(x => x.ImportId == importId && x.Id == rowId, cancellationToken);
        if (row is null)
        {
            return false;
        }

        var resolved = await ResolveClientAsync(clientUserId, clientDisplayName, cancellationToken);
        if (resolved is null)
        {
            return false;
        }

        row.ClientUserId = resolved.Value.UserId;
        row.ClientDisplayName = resolved.Value.DisplayName;
        row.MatchStatus = "MANUAL";
        row.CandidateClientIdsJson = JsonSerializer.Serialize(new[] { resolved.Value.UserId });

        var import = await _dbContext.BankImports.SingleAsync(x => x.Id == importId, cancellationToken);
        import.MatchedRows = await _dbContext.BankImportRows.CountAsync(
            x => x.ImportId == importId && (x.MatchStatus == "MATCHED" || x.MatchStatus == "MANUAL"),
            cancellationToken);
        import.AmbiguousRows = await _dbContext.BankImportRows.CountAsync(
            x => x.ImportId == importId && x.MatchStatus == "AMBIGUOUS",
            cancellationToken);
        import.InvalidRows = await _dbContext.BankImportRows.CountAsync(
            x => x.ImportId == importId && x.MatchStatus == "INVALID",
            cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogService.WriteAsync(
            "payments.import.manual-match",
            "bank_import_row",
            row.Id,
            actorUserId,
            actorRole,
            $$"""{"clientUserId":"{{resolved.Value.UserId}}"}""",
            cancellationToken);

        return true;
    }

    private static string ComputeSha256(byte[] payload)
    {
        var hash = SHA256.HashData(payload);
        return Convert.ToHexString(hash);
    }

    private async Task<Dictionary<string, string>> LoadClientLookupAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.ClientProfiles
            .AsNoTracking()
            .ToDictionaryAsync(
                x => x.UserId,
                x => string.IsNullOrWhiteSpace(x.FullName) ? x.UserId : x.FullName,
                StringComparer.OrdinalIgnoreCase,
                cancellationToken);
    }

    private async Task<(string UserId, string DisplayName)?> ResolveClientAsync(
        string? clientUserId,
        string? clientDisplayName,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(clientUserId))
        {
            var byId = await _dbContext.ClientProfiles
                .AsNoTracking()
                .Where(x => x.UserId == clientUserId)
                .Select(x => new { x.UserId, x.FullName })
                .SingleOrDefaultAsync(cancellationToken);

            if (byId is not null)
            {
                return (byId.UserId, string.IsNullOrWhiteSpace(byId.FullName) ? byId.UserId : byId.FullName);
            }
        }

        if (!string.IsNullOrWhiteSpace(clientDisplayName))
        {
            var normalized = clientDisplayName.Trim();
            var byName = await _dbContext.ClientProfiles
                .AsNoTracking()
                .Where(x => x.FullName == normalized)
                .Select(x => new { x.UserId, x.FullName })
                .ToArrayAsync(cancellationToken);

            if (byName.Length == 1)
            {
                return (byName[0].UserId, byName[0].FullName);
            }
        }

        return null;
    }

    private static List<BankImportRow> BuildRows(
        IReadOnlyList<Payment1CDocument> documents,
        Dictionary<string, string> clientLookup)
    {
        var now = DateTime.UtcNow;
        var rows = new List<BankImportRow>(documents.Count);

        for (var i = 0; i < documents.Count; i++)
        {
            var document = documents[i];
            var status = ResolveStatus(document, clientLookup, out var resolvedClientUserId, out var resolvedClientName, out var candidateIds);

            rows.Add(new BankImportRow
            {
                Id = Guid.NewGuid(),
                Reference = document.Fields.DocNo ?? $"ROW-{i + 1:000}",
                Amount = document.Fields.Amount ?? 0m,
                PaymentDateUtc = document.Fields.DocDate ?? now.Date,
                MatchStatus = status,
                ClientUserId = resolvedClientUserId,
                ClientDisplayName = resolvedClientName,
                CandidateClientIdsJson = JsonSerializer.Serialize(candidateIds),
                DocType = document.Fields.DocType,
                DocNo = document.Fields.DocNo,
                DocDateUtc = document.Fields.DocDate,
                PayerName = document.Fields.PayerName,
                PayerInn = document.Fields.PayerInn,
                PayerAccount = document.Fields.PayerAccount,
                ReceiverAccount = document.Fields.ReceiverAccount,
                Purpose = document.Fields.Purpose,
                ExtraJson = JsonSerializer.Serialize(document.Extra),
            });
        }

        return rows;
    }

    private static string ResolveStatus(
        Payment1CDocument document,
        Dictionary<string, string> clientLookup,
        out string? resolvedClientUserId,
        out string? resolvedClientName,
        out IReadOnlyCollection<string> candidates)
    {
        resolvedClientUserId = null;
        resolvedClientName = null;

        var hasAmount = document.Fields.Amount.HasValue && document.Fields.Amount.Value > 0m;
        var hasDate = document.Fields.DocDate.HasValue;
        if (!hasAmount || !hasDate)
        {
            candidates = [];
            return "INVALID";
        }

        var keys = PaymentPurposeMatchers.ExtractClientKeys(document.Fields.Purpose);
        var found = keys
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Where(clientLookup.ContainsKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        candidates = found;

        if (found.Length == 1)
        {
            resolvedClientUserId = found[0];
            resolvedClientName = clientLookup[found[0]];
            return "MATCHED";
        }

        if (found.Length > 1)
        {
            return "AMBIGUOUS";
        }

        return "UNMATCHED";
    }
}
