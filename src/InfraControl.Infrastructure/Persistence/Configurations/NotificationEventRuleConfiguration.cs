using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

/// <summary>
/// F42-T001: regra por tipo de evento (linha única por EventType; sem
/// linha = fanout global atual).
/// </summary>
public sealed class NotificationEventRuleConfiguration
    : IEntityTypeConfiguration<NotificationEventRule>
{
    public void Configure(EntityTypeBuilder<NotificationEventRule> builder)
    {
        builder.ToTable("notification_event_rules");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.EventType)
            .HasColumnName("event_type")
            .HasMaxLength(NotificationEventRule.MaxEventTypeLength)
            .IsRequired();

        builder.HasIndex(x => x.EventType)
            .IsUnique();

        builder.Property(x => x.Enabled)
            .HasColumnName("enabled")
            .IsRequired();

        builder.Property(x => x.Channels)
            .HasColumnName("channels")
            .HasMaxLength(NotificationEventRule.MaxChannelsLength)
            .IsRequired();

        builder.Property(x => x.CreateDateTime)
            .HasColumnName("create_date_time");

        builder.Property(x => x.UpdateDateTime)
            .HasColumnName("update_date_time");
    }
}
