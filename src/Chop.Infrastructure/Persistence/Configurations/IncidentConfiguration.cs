using Chop.Domain.Incidents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chop.Infrastructure.Persistence.Configurations;

internal sealed class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> builder)
    {
        builder.ToTable("incidents");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ClientUserId)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.AddressText)
            .HasMaxLength(512);

        builder.Property(x => x.GeoPoint)
            .HasColumnName("GeoPoint")
            .HasSrid(4326);

        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.LastUpdatedAtUtc).IsRequired();

        builder.HasMany(x => x.StatusHistory)
            .WithOne(x => x.Incident)
            .HasForeignKey(x => x.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ClientProfile)
            .WithMany()
            .HasForeignKey(x => x.ClientUserId)
            .HasPrincipalKey("UserId")
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CreatedAtUtc);
        builder.HasIndex(x => x.GeoPoint);
    }
}
