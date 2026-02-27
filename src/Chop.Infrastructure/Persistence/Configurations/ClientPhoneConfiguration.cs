using Chop.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chop.Infrastructure.Persistence.Configurations;

internal sealed class ClientPhoneConfiguration : IEntityTypeConfiguration<ClientPhone>
{
    public void Configure(EntityTypeBuilder<ClientPhone> builder)
    {
        builder.ToTable("client_phones");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Phone)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Type)
            .HasMaxLength(32)
            .IsRequired();

        builder.HasIndex(x => x.ClientProfileId);
    }
}
