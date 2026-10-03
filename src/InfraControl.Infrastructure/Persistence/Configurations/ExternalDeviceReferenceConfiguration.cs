using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

/// <summary>
/// F19-T006: identidade externa estável do device — unicidade física em
/// (source, external_id) como âncora do lookup "MAC/Device ID → Device" e
/// FK em devices com cascade (apagar o device apaga suas referências).
/// </summary>
public sealed class ExternalDeviceReferenceConfiguration
    : IEntityTypeConfiguration<ExternalDeviceReference>
{
    public void Configure(EntityTypeBuilder<ExternalDeviceReference> builder)
    {
        builder.ToTable("external_device_references");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.Source)
            .HasColumnName("source")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.ExternalId)
            .HasColumnName("external_id")
            .HasMaxLength(ExternalDeviceReference.ExternalIdMaxLength)
            .IsRequired();

        builder.Property(x => x.DeviceId)
            .HasColumnName("device_id")
            .IsRequired();

        builder.HasIndex(x => new { x.Source, x.ExternalId })
            .IsUnique()
            .HasDatabaseName("IX_external_device_references_source_external_id");

        builder.HasOne<Device>()
            .WithMany()
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
