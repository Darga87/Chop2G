using Chop.Domain.Guards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chop.Infrastructure.Persistence.Configurations;

internal sealed class SecurityPointConfiguration : IEntityTypeConfiguration<SecurityPoint>
{
    public void Configure(EntityTypeBuilder<SecurityPoint> builder)
    {
        builder.ToTable("security_points");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.Label)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Type)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Address)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.GeoPoint)
            .HasColumnName("GeoPoint")
            .HasSrid(4326);

        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();

        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.Type);
        builder.HasIndex(x => x.GeoPoint);
    }
}

