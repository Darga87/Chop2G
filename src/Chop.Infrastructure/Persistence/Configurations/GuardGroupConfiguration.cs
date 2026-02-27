using Chop.Domain.Guards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chop.Infrastructure.Persistence.Configurations;

internal sealed class GuardGroupConfiguration : IEntityTypeConfiguration<GuardGroup>
{
    public void Configure(EntityTypeBuilder<GuardGroup> builder)
    {
        builder.ToTable("guard_groups");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();

        builder.HasIndex(x => x.Name)
            .IsUnique();
    }
}

