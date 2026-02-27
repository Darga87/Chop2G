using Chop.Domain.Guards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chop.Infrastructure.Persistence.Configurations;

internal sealed class GuardLocationConfiguration : IEntityTypeConfiguration<GuardLocation>
{
    public void Configure(EntityTypeBuilder<GuardLocation> builder)
    {
        builder.ToTable("guard_locations");
        builder.HasKey(x => x.GuardUserId);

        builder.Property(x => x.GuardUserId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.Latitude).IsRequired();
        builder.Property(x => x.Longitude).IsRequired();
        builder.Property(x => x.GeoPoint)
            .HasColumnName("GeoPoint")
            .HasSrid(4326);
        builder.Property(x => x.AccuracyMeters);
        builder.Property(x => x.DeviceTimeUtc);

        builder.Property(x => x.ShiftId)
            .HasMaxLength(128);

        builder.Property(x => x.UpdatedAtUtc).IsRequired();

        builder.HasIndex(x => x.IncidentId);
        builder.HasIndex(x => x.UpdatedAtUtc);
        builder.HasIndex(x => x.GeoPoint);
    }
}
