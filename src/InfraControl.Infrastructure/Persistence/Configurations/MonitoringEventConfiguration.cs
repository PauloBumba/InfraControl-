using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

/// <summary>
/// ADR-027: evento externo (Zabbix) — linha por evento recebido, com
/// unicidade (source, external_event_id) como âncora de dedup e o lifecycle
/// do problem correlacionado em (source, external_problem_id).
/// </summary>
public sealed class MonitoringEventConfiguration : IEntityTypeConfiguration<MonitoringEvent>
{
    public void Configure(EntityTypeBuilder<MonitoringEvent> builder)
    {
        builder.ToTable("monitoring_events");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.Source)
            .HasColumnName("source")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.ExternalEventId)
            .HasColumnName("external_event_id")
            .HasMaxLength(MonitoringEvent.ExternalIdMaxLength)
            .IsRequired();

        builder.Property(x => x.ExternalProblemId)
            .HasColumnName("external_problem_id")
            .HasMaxLength(MonitoringEvent.ExternalIdMaxLength)
            .IsRequired();

        builder.Property(x => x.ExternalRecoveryId)
            .HasColumnName("external_recovery_id")
            .HasMaxLength(MonitoringEvent.ExternalIdMaxLength);

        builder.Property(x => x.DeviceId)
            .HasColumnName("device_id");

        builder.Property(x => x.CheckType)
            .HasColumnName("check_type")
            .HasMaxLength(MonitoringEvent.CheckTypeMaxLength);

        builder.Property(x => x.Severity)
            .HasColumnName("severity")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.OccurredAt)
            .HasColumnName("occurred_at");

        builder.Property(x => x.RecoveredAt)
            .HasColumnName("recovered_at");

        builder.Property(x => x.Message)
            .HasColumnName("message")
            .HasMaxLength(MonitoringEvent.MessageMaxLength)
            .IsRequired();

        builder.Property(x => x.RawPayload)
            .HasColumnName("raw_payload");

        // Evidência bruta sobrevive à exclusão do device (SetNull) — ao
        // contrário do Alert, que acompanha o device (cascade).
        builder.HasOne<Device>()
            .WithMany()
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.Source, x.ExternalEventId })
            .IsUnique()
            .HasDatabaseName("IX_monitoring_events_source_external_event_id");

        builder.HasIndex(x => new { x.Source, x.ExternalProblemId })
            .HasDatabaseName("IX_monitoring_events_source_external_problem_id");

        // Cutoff da retenção (CleanupService) filtra por OccurredAt — sem
        // este índice o delete faria full scan na tabela de eventos.
        builder.HasIndex(x => x.OccurredAt)
            .HasDatabaseName("IX_monitoring_events_occurred_at");
    }
}
