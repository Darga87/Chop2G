using Chop.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chop.Infrastructure.Persistence.Configurations;

internal sealed class ClientProfileConfiguration : IEntityTypeConfiguration<ClientProfile>
{
    public void Configure(EntityTypeBuilder<ClientProfile> builder)
    {
        builder.ToTable("client_profiles");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.FullName)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(x => x.Tariff)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.BillingStatus)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(x => x.LastPaymentAtUtc)
            .IsRequired();

        builder.Property(x => x.HasDebt)
            .IsRequired();

        builder.HasIndex(x => x.UserId)
            .IsUnique();

        builder.HasMany(x => x.Phones)
            .WithOne(x => x.ClientProfile)
            .HasForeignKey(x => x.ClientProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Addresses)
            .WithOne(x => x.ClientProfile)
            .HasForeignKey(x => x.ClientProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
