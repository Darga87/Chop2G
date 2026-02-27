using Chop.Domain.Alerts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chop.Infrastructure.Persistence.Configurations;

internal sealed class AlertEventConfiguration : IEntityTypeConfiguration<AlertEvent>
{
    public void Configure(EntityTypeBuilder<AlertEvent> builder)
    {
        builder.ToTable("alert_events");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RuleCode)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.Severity)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.EntityType)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.PayloadJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc).IsRequired();

        builder.Property(x => x.AckedAtUtc);
        builder.Property(x => x.AckedByUserId).HasMaxLength(128);
        builder.Property(x => x.ResolvedAtUtc);
        builder.Property(x => x.ResolvedByUserId).HasMaxLength(128);

        builder.HasIndex(x => x.RuleCode);
        builder.HasIndex(x => new { x.Severity, x.CreatedAtUtc });
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
        builder.HasIndex(x => new { x.Status, x.CreatedAtUtc });
    }
}
