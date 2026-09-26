using System.Text.Json;
using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class DiscoveryResultConfiguration : IEntityTypeConfiguration<DiscoveryResult>
{
    public void Configure(EntityTypeBuilder<DiscoveryResult> builder)
    {
        builder.ToTable("discovery_results");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.DiscoveryRunId)
            .HasColumnName("discovery_run_id")
            .IsRequired();

        builder.Property(x => x.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Summary)
            .HasColumnName("summary")
            .HasMaxLength(DiscoveryResult.SummaryMaxLength)
            .IsRequired();

        builder.Property(x => x.DeviceId)
            .HasColumnName("device_id");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Mesmo padrão do MonitoringResult.Values — JSON no banco.
        var valuesConverter = new ValueConverter<IReadOnlyDictionary<string, string>?, string?>(
            values => values == null
                ? null
                : JsonSerializer.Serialize(values),
            json => json == null
                ? null
                : JsonSerializer.Deserialize<Dictionary<string, string>>(json));

        var valuesComparer = new ValueComparer<IReadOnlyDictionary<string, string>?>(
            (a, b) => a == null && b == null
                || a != null && b != null
                   && a.Count == b.Count
                   && !a.Except(b).Any(),
            values => values == null
                ? 0
                : values.Aggregate(
                    0,
                    (hash, pair) => HashCode.Combine(hash, pair.Key.GetHashCode(), pair.Value.GetHashCode())),
            values => values);

        builder.Property(x => x.Values)
            .HasColumnName("values_json")
            .HasConversion(valuesConverter);
        builder.Property(x => x.Values)
            .Metadata.SetValueComparer(valuesComparer);

        // Cascata com o run: excluir run (ou o device dono do run) apaga
        // os achados junto (mesma decisão do histórico de monitoramento).
        builder.HasOne<DiscoveryRun>()
            .WithMany()
            .HasForeignKey(x => x.DiscoveryRunId)
            .OnDelete(DeleteBehavior.Cascade);

        // Device opcional (T010): pode apontar para OUTRO device conhecido;
        // se ele for excluído, o achado fica órfão de vínculo (SetNull).
        builder.HasOne<Device>()
            .WithMany()
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.DiscoveryRunId);
    }
}
