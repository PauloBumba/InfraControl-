using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.ToTable("alerts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        // ADR-027: nullable — alerta externo de host não mapeado não tem
        // device. Nativo sempre preenche (validado em Alert.Create).
        builder.Property(x => x.DeviceId)
            .HasColumnName("device_id");

        builder.Property(x => x.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.Source)
            .HasColumnName("source")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.MonitoringEventId)
            .HasColumnName("monitoring_event_id");

        builder.Property(x => x.ExternalProblemId)
            .HasColumnName("external_problem_id")
            .HasMaxLength(Alert.ExternalIdMaxLength);

        builder.Property(x => x.Severity)
            .HasColumnName("severity")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Message)
            .HasColumnName("message")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.ResolvedAt)
            .HasColumnName("resolved_at");

        builder.HasOne<Device>()
            .WithMany()
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        // ADR-027: o alerta referencia a evidência; a remoção do evento
        // (limpeza futura) não apaga o alerta histórico.
        builder.HasOne<MonitoringEvent>()
            .WithMany()
            .HasForeignKey(x => x.MonitoringEventId)
            .OnDelete(DeleteBehavior.SetNull);

        // Device + check juntos no histórico; a unicidade de Open é lógica
        // (1 por par) garantida pelo avaliador.
        builder.HasIndex(x => new { x.DeviceId, x.Kind })
            .HasDatabaseName("IX_alerts_device_id_kind");

        // ADR-027: chave de dedup/recovery externo por fonte.
        builder.HasIndex(x => new { x.Source, x.ExternalProblemId })
            .HasDatabaseName("IX_alerts_source_external_problem_id");
    }
}
