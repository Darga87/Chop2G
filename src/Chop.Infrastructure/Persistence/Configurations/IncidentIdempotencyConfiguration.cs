using Chop.Domain.Incidents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chop.Infrastructure.Persistence.Configurations;

internal sealed class IncidentIdempotencyConfiguration : IEntityTypeConfiguration<IncidentIdempotency>
{
    public void Configure(EntityTypeBuilder<IncidentIdempotency> builder)
    {
        builder.ToTable("incident_idempotency");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ClientUserId)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.IdempotencyKey)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.RequestHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.ExpiresAtUtc)
            .IsRequired();

        builder.HasIndex(x => new { x.ClientUserId, x.IdempotencyKey })
            .IsUnique();

        builder.HasIndex(x => new { x.ClientUserId, x.CreatedAtUtc });
        builder.HasIndex(x => x.ExpiresAtUtc);
    }
}
