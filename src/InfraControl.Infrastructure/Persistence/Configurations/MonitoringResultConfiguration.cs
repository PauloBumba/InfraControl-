using System.Text.Json;
using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class MonitoringResultConfiguration : IEntityTypeConfiguration<MonitoringResult>
{
    public void Configure(EntityTypeBuilder<MonitoringResult> builder)
    {
        builder.ToTable("monitoring_results");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.DeviceId)
            .HasColumnName("device_id")
            .IsRequired();

        builder.Property(x => x.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Succeeded)
            .HasColumnName("succeeded")
            .IsRequired();

        builder.Property(x => x.DurationMs)
            .HasColumnName("duration_ms")
            .IsRequired();

        builder.Property(x => x.Error)
            .HasColumnName("error")
            .HasMaxLength(MonitoringResult.ErrorMaxLength);

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
                    (hash, pair) => HashCode.Combine(
                        hash, pair.Key.GetHashCode(), pair.Value.GetHashCode())),
            values => values);

        builder.Property(x => x.Values)
            .HasColumnName("values_json")
            .HasConversion(valuesConverter);
        builder.Property(x => x.Values)
            .Metadata.SetValueComparer(valuesComparer);

        // Histórico é do device (sobrevive a mudanças de perfil); exclusão do
        // device apaga os resultados junto (cascade).
        builder.HasOne<Device>()
            .WithMany()
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Leitura por device, mais recente primeiro.
        builder.HasIndex(x => new { x.DeviceId, x.CreateDateTime })
            .HasDatabaseName("IX_monitoring_results_device_id_CreateDateTime");
    }
}
