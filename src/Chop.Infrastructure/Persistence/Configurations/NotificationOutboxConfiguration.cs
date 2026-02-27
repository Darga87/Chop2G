using Chop.Domain.Alerts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chop.Infrastructure.Persistence.Configurations;

internal sealed class NotificationOutboxConfiguration : IEntityTypeConfiguration<NotificationOutbox>
{
    public void Configure(EntityTypeBuilder<NotificationOutbox> builder)
    {
        builder.ToTable("notification_outbox");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Channel)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.Destination)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.PayloadJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.AttemptCount).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();

        builder.HasMany(x => x.Deliveries)
            .WithOne(x => x.Outbox)
            .HasForeignKey(x => x.OutboxId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.Status, x.NextAttemptAtUtc });
        builder.HasIndex(x => x.CreatedAtUtc);
    }
}
