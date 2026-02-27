using Chop.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chop.Infrastructure.Persistence.Configurations;

internal sealed class ClientAddressConfiguration : IEntityTypeConfiguration<ClientAddress>
{
    public void Configure(EntityTypeBuilder<ClientAddress> builder)
    {
        builder.ToTable("client_addresses");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Label)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.AddressText)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(x => x.GeoPoint)
            .HasColumnName("GeoPoint")
            .HasSrid(4326);

        builder.HasIndex(x => x.ClientProfileId);
        builder.HasIndex(x => x.GeoPoint);
    }
}
