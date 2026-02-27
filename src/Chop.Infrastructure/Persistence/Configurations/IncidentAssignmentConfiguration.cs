using Chop.Domain.Incidents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chop.Infrastructure.Persistence.Configurations;

internal sealed class IncidentAssignmentConfiguration : IEntityTypeConfiguration<IncidentAssignment>
{
    public void Configure(EntityTypeBuilder<IncidentAssignment> builder)
    {
        builder.ToTable("incident_assignments");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.GuardUserId)
            .HasMaxLength(128);

        builder.Property(x => x.PatrolUnitId)
            .HasMaxLength(128);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc).IsRequired();

        builder.HasOne(x => x.Incident)
            .WithMany(x => x.Assignments)
            .HasForeignKey(x => x.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.IncidentId);
        builder.HasIndex(x => x.GuardUserId);
        builder.HasIndex(x => x.PatrolUnitId);
    }
}
