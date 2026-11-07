using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

/// <summary>
/// F39-T003: regras de roteamento por EventType — várias condicionais por
/// tipo (índice NÃO único), filtros de severidade/host/grupo/trigger/tags e
/// canais substitutivos.
/// </summary>
public sealed class NotificationRoutingRuleConfiguration
    : IEntityTypeConfiguration<NotificationRoutingRule>
{
    public void Configure(EntityTypeBuilder<NotificationRoutingRule> builder)
    {
        builder.ToTable("notification_routing_rules");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.EventType)
            .HasColumnName("event_type")
            .HasMaxLength(NotificationRoutingRule.MaxEventTypeLength)
            .IsRequired();

        builder.HasIndex(x => x.EventType);

        builder.Property(x => x.Enabled)
            .HasColumnName("enabled")
            .IsRequired();

        builder.Property(x => x.MinSeverity)
            .HasColumnName("min_severity");

        builder.Property(x => x.MaxSeverity)
            .HasColumnName("max_severity");

        builder.Property(x => x.HostIds)
            .HasColumnName("host_ids")
            .HasMaxLength(NotificationRoutingRule.MaxIdListLength)
            .IsRequired();

        builder.Property(x => x.HostGroupIds)
            .HasColumnName("host_group_ids")
            .HasMaxLength(NotificationRoutingRule.MaxIdListLength)
            .IsRequired();

        builder.Property(x => x.TriggerId)
            .HasColumnName("trigger_id")
            .HasMaxLength(NotificationRoutingRule.MaxTriggerIdLength);

        builder.Property(x => x.Tags)
            .HasColumnName("tags")
            .HasMaxLength(NotificationRoutingRule.MaxTagsLength)
            .IsRequired();

        builder.Property(x => x.Channels)
            .HasColumnName("channels")
            .HasMaxLength(NotificationRoutingRule.MaxChannelsLength)
            .IsRequired();

        builder.Property(x => x.CreateDateTime)
            .HasColumnName("create_date_time");

        builder.Property(x => x.UpdateDateTime)
            .HasColumnName("update_date_time");
    }
}
