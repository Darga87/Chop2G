using Chop.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chop.Infrastructure.Persistence.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ClientUserId).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Source).IsRequired().HasMaxLength(32);
        builder.Property(x => x.ExternalReference).HasMaxLength(128);

        builder.HasIndex(x => x.ClientUserId);
        builder.HasIndex(x => x.ImportId);
        builder.HasIndex(x => x.ImportRowId).IsUnique();
        builder.HasIndex(x => x.PaidAtUtc);
    }
}

