using Chop.Domain.Incidents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chop.Infrastructure.Persistence.Configurations;

internal sealed class IncidentStatusHistoryConfiguration : IEntityTypeConfiguration<IncidentStatusHistory>
{
    public void Configure(EntityTypeBuilder<IncidentStatusHistory> builder)
    {
        builder.ToTable("incident_status_history");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FromStatus)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(x => x.ToStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.ActorUserId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.ActorRole)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.Comment)
            .HasMaxLength(1024);

        builder.Property(x => x.CreatedAtUtc).IsRequired();

        builder.HasIndex(x => x.IncidentId);
        builder.HasIndex(x => x.CreatedAtUtc);
    }
}
