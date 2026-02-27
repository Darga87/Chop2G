using Chop.Domain.Alerts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Chop.Infrastructure.Persistence.Configurations;

internal sealed class AlertRuleConfiguration : IEntityTypeConfiguration<AlertRule>
{
    public void Configure(EntityTypeBuilder<AlertRule> builder)
    {
        builder.ToTable("alert_rules");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.IsEnabled).IsRequired();

        builder.Property(x => x.SettingsJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc).IsRequired();

        builder.HasIndex(x => x.Code).IsUnique();
    }
}
