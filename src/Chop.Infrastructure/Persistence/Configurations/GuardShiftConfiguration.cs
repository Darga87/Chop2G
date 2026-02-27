using Chop.Domain.Guards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chop.Infrastructure.Persistence.Configurations;

internal sealed class GuardShiftConfiguration : IEntityTypeConfiguration<GuardShift>
{
    public void Configure(EntityTypeBuilder<GuardShift> builder)
    {
        builder.ToTable("guard_shifts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.GuardUserId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.StartedAtUtc).IsRequired();

        builder.HasOne(x => x.GuardGroup)
            .WithMany()
            .HasForeignKey(x => x.GuardGroupId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.SecurityPoint)
            .WithMany()
            .HasForeignKey(x => x.SecurityPointId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.GuardUserId);
        builder.HasIndex(x => x.StartedAtUtc);
        builder.HasIndex(x => x.EndedAtUtc);
    }
}

