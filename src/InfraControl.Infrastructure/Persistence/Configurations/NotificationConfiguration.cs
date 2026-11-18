using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.EventId)
            .HasColumnName("event_id")
            .IsRequired();

        builder.Property(x => x.EventType)
            .HasColumnName("event_type")
            .HasMaxLength(Notification.EventTypeMaxLength)
            .IsRequired();

        builder.Property(x => x.Title)
            .HasColumnName("title")
            .HasMaxLength(Notification.TitleMaxLength)
            .IsRequired();

        builder.Property(x => x.Message)
            .HasColumnName("message")
            .HasMaxLength(Notification.MessageMaxLength)
            .IsRequired();

        builder.Property(x => x.DeviceId)
            .HasColumnName("device_id");

        builder.Property(x => x.SourceAlertId)
            .HasColumnName("source_alert_id");

        builder.Property(x => x.SourceIncidentId)
            .HasColumnName("source_incident_id");

        // Correlação abrir↔resolver do mesmo agregado (anti-flapping).
        builder.Property(x => x.AggregateId)
            .HasColumnName("aggregate_id");

        builder.HasIndex(x => x.AggregateId)
            .HasDatabaseName("ix_notifications_aggregate_id");

        builder.Property(x => x.CreateDateTime)
            .HasColumnName("create_datetime")
            .IsRequired();

        builder.Property(x => x.UpdateDateTime)
            .HasColumnName("update_datetime")
            .IsRequired();

        // Idempotência visual: um evento gera no máximo uma notificação.
        builder.HasIndex(x => x.EventId)
            .IsUnique()
            .HasDatabaseName("ux_notifications_event_id");
    }
}
