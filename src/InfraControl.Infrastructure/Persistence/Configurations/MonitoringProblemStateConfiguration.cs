using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

/// <summary>
/// ADR-068: estado do problema lógico — unicidade (source,
/// correlation_key) é a âncora anti-corrida da máquina de estados;
/// AlertId indexado porque o caminho do OK correlaciona por ele.
/// </summary>
public sealed class MonitoringProblemStateConfiguration
    : IEntityTypeConfiguration<MonitoringProblemState>
{
    public void Configure(EntityTypeBuilder<MonitoringProblemState> builder)
    {
        builder.ToTable("monitoring_problem_states");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.Source)
            .HasColumnName("source")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.CorrelationKey)
            .HasColumnName("correlation_key")
            .HasMaxLength(MonitoringProblemState.CorrelationKeyMaxLength)
            .IsRequired();

        builder.Property(x => x.DeviceId)
            .HasColumnName("device_id");

        builder.Property(x => x.Severity)
            .HasColumnName("severity")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.AlertId)
            .HasColumnName("alert_id");

        builder.Property(x => x.PreviousAlertId)
            .HasColumnName("previous_alert_id");

        builder.Property(x => x.IncidentId)
            .HasColumnName("incident_id");

        builder.Property(x => x.LastProblemEventId)
            .HasColumnName("last_problem_event_id")
            .HasMaxLength(MonitoringProblemState.ExternalIdMaxLength);

        builder.Property(x => x.OpenCount)
            .HasColumnName("open_count")
            .IsRequired();

        builder.Property(x => x.RepeatedCount)
            .HasColumnName("repeated_count")
            .IsRequired();

        builder.Property(x => x.OpenedAt)
            .HasColumnName("opened_at")
            .IsRequired();

        builder.Property(x => x.ResolvedAt)
            .HasColumnName("resolved_at");

        builder.HasIndex(x => new { x.Source, x.CorrelationKey })
            .IsUnique()
            .HasDatabaseName("IX_monitoring_problem_states_source_correlation_key");

        builder.HasIndex(x => x.AlertId)
            .HasDatabaseName("IX_monitoring_problem_states_alert_id");
    }
}
