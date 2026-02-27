using Chop.Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chop.Infrastructure.Persistence.Configurations;

internal sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("audit_log");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ActorUserId)
            .HasMaxLength(128);

        builder.Property(x => x.ActorRole)
            .HasMaxLength(32);

        builder.Property(x => x.Action)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.EntityType)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.ChangesJson)
            .HasColumnType("jsonb");

        builder.Property(x => x.CreatedAtUtc).IsRequired();

        builder.HasIndex(x => x.CreatedAtUtc);
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
    }
}
