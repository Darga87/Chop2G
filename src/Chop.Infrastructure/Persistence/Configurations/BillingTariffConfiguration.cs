using Chop.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chop.Infrastructure.Persistence.Configurations;

internal sealed class BillingTariffConfiguration : IEntityTypeConfiguration<BillingTariff>
{
    public void Configure(EntityTypeBuilder<BillingTariff> builder)
    {
        builder.ToTable("billing_tariffs");
        builder.HasKey(x => x.Code);

        builder.Property(x => x.Code)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(1024);

        builder.Property(x => x.MonthlyFee)
            .HasColumnType("numeric(18,2)");

        builder.Property(x => x.Currency)
            .HasMaxLength(8)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.HasIndex(x => x.IsActive);
        builder.HasIndex(x => x.SortOrder);
    }
}
