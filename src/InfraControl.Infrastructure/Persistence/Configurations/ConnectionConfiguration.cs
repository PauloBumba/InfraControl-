using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class ConnectionConfiguration : IEntityTypeConfiguration<Connection>
{
    // F53-T001: origem/confiança persistem em MINÚSCULAS (decisão
    // registrada; mesmo casing do UniFi live — "lldp"/"confirmed").
    // Unknown = 0 no enum: com HasDefaultValue, valor == default do CLR
    // sai do INSERT e o DEFAULT 'unknown' do banco assume (backfill das
    // linhas legadas), enquanto Manual/Confirmed = 1 sempre são gravados.
    private static readonly ValueConverter<ConnectionSource, string> SourceConverter =
        new(
            source => source.ToString().ToLowerInvariant(),
            value => ParseMemberName<ConnectionSource>(value));

    private static readonly ValueConverter<ConnectionConfidence, string> ConfidenceConverter =
        new(
            confidence => confidence.ToString().ToLowerInvariant(),
            value => ParseMemberName<ConnectionConfidence>(value));

    public void Configure(EntityTypeBuilder<Connection> builder)
    {
        builder.ToTable("connections");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.SourceInterfaceId)
            .HasColumnName("source_interface_id")
            .IsRequired();

        builder.Property(x => x.TargetInterfaceId)
            .HasColumnName("target_interface_id")
            .IsRequired();

        builder.Property(x => x.Source)
            .HasColumnName("source")
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(ConnectionSource.Unknown)
            .HasConversion(SourceConverter);

        builder.Property(x => x.Confidence)
            .HasColumnName("confidence")
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue(ConnectionConfidence.Unknown)
            .HasConversion(ConfidenceConverter);

        // Backstop: exclusão de interface com conexão bloqueada no handler (409).
        builder.HasOne<NetworkInterface>()
            .WithMany()
            .HasForeignKey(x => x.SourceInterfaceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<NetworkInterface>()
            .WithMany()
            .HasForeignKey(x => x.TargetInterfaceId)
            .OnDelete(DeleteBehavior.Restrict);

        // Par DIRIGIDO único: (a,b) é diferente de (b,a).
        builder.HasIndex(x => new { x.SourceInterfaceId, x.TargetInterfaceId })
            .IsUnique();
    }

    /// <summary>
    /// Só nome de membro ("manual", "LLDP"…) — representação numérica não
    /// é contrato (ADR-061). Valor ausente/corrompido → Unknown (spec 11.7:
    /// sem informação suficiente, apresentar como desconhecida).
    /// </summary>
    private static T ParseMemberName<T>(string? value) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
            return default;

        var token = value.Trim();
        if (char.IsDigit(token[0]) || token[0] is '-' or '+')
            return default;

        return Enum.TryParse<T>(token, ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed)
            ? parsed
            : default;
    }
}
