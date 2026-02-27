using Chop.Domain.Incidents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chop.Infrastructure.Persistence.Configurations;

internal sealed class DispatchRecipientConfiguration : IEntityTypeConfiguration<DispatchRecipient>
{
    public void Configure(EntityTypeBuilder<DispatchRecipient> builder)
    {
        builder.ToTable("dispatch_recipients");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RecipientType)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.RecipientId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.AcceptedBy)
            .HasMaxLength(128);

        builder.Property(x => x.AcceptedVia)
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.HasIndex(x => new { x.DispatchId, x.RecipientType, x.RecipientId });
    }
}
