using Chop.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chop.Infrastructure.Persistence.Configurations;

internal sealed class BankImportConfiguration : IEntityTypeConfiguration<BankImport>
{
    public void Configure(EntityTypeBuilder<BankImport> builder)
    {
        builder.ToTable("bank_imports");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FileName).IsRequired().HasMaxLength(256);
        builder.Property(x => x.FileHash).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(32);
        builder.Property(x => x.CreatedByUserId).HasMaxLength(128);
        builder.Property(x => x.AppliedByUserId).HasMaxLength(128);

        builder.HasIndex(x => x.FileHash).IsUnique();
        builder.HasIndex(x => x.CreatedAtUtc);
    }
}

