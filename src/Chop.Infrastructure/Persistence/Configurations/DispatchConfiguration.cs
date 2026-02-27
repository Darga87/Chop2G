using Chop.Domain.Incidents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chop.Infrastructure.Persistence.Configurations;

internal sealed class DispatchConfiguration : IEntityTypeConfiguration<Dispatch>
{
    public void Configure(EntityTypeBuilder<Dispatch> builder)
    {
        builder.ToTable("dispatches");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CreatedByUserId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.Method)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.Comment)
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedAtUtc).IsRequired();

        builder.HasOne(x => x.Incident)
            .WithMany(x => x.Dispatches)
            .HasForeignKey(x => x.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Recipients)
            .WithOne(x => x.Dispatch)
            .HasForeignKey(x => x.DispatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.IncidentId);
        builder.HasIndex(x => x.CreatedAtUtc);
    }
}
