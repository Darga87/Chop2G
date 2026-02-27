using Chop.Domain.Guards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chop.Infrastructure.Persistence.Configurations;

internal sealed class GuardGroupMemberConfiguration : IEntityTypeConfiguration<GuardGroupMember>
{
    public void Configure(EntityTypeBuilder<GuardGroupMember> builder)
    {
        builder.ToTable("guard_group_members");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.GuardUserId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.IsCommander).IsRequired();
        builder.Property(x => x.AddedAtUtc).IsRequired();

        builder.HasOne(x => x.GuardGroup)
            .WithMany(x => x.Members)
            .HasForeignKey(x => x.GuardGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.GuardGroupId, x.GuardUserId })
            .IsUnique();
    }
}

