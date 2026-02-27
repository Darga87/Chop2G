using Chop.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chop.Infrastructure.Persistence.Configurations;

internal sealed class BankImportRowConfiguration : IEntityTypeConfiguration<BankImportRow>
{
    public void Configure(EntityTypeBuilder<BankImportRow> builder)
    {
        builder.ToTable("bank_import_rows");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Reference).IsRequired().HasMaxLength(128);
        builder.Property(x => x.MatchStatus).IsRequired().HasMaxLength(32);
        builder.Property(x => x.ClientUserId).HasMaxLength(128);
        builder.Property(x => x.ClientDisplayName).HasMaxLength(256);
        builder.Property(x => x.CandidateClientIdsJson).HasColumnType("jsonb");
        builder.Property(x => x.DocType).IsRequired().HasMaxLength(64);
        builder.Property(x => x.DocNo).HasMaxLength(128);
        builder.Property(x => x.PayerName).HasMaxLength(256);
        builder.Property(x => x.PayerInn).HasMaxLength(32);
        builder.Property(x => x.PayerAccount).HasMaxLength(64);
        builder.Property(x => x.ReceiverAccount).HasMaxLength(64);
        builder.Property(x => x.Purpose).HasMaxLength(2048);
        builder.Property(x => x.ExtraJson).IsRequired().HasColumnType("jsonb");

        builder.HasIndex(x => x.ImportId);
        builder.HasIndex(x => x.MatchStatus);
        builder.HasIndex(x => x.ClientUserId);

        builder.HasOne(x => x.Import)
            .WithMany(x => x.Rows)
            .HasForeignKey(x => x.ImportId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

