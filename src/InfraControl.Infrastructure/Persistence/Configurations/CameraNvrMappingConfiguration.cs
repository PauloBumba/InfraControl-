using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

/// <summary>
/// F30-T005c: camera_nvr_mappings — câmera (FK Restrict na exclusão via
/// pre-check do DeleteDevice, mas Cascade aqui: excluir a câmera remove o
/// vínculo) e NVR (FK Restrict: excluir NVR com mapeamentos → 409 no handler,
/// backstop no banco). Unicidade (nvr_id, channel_number) — docs/46.
/// </summary>
public sealed class CameraNvrMappingConfiguration : IEntityTypeConfiguration<CameraNvrMapping>
{
    public void Configure(EntityTypeBuilder<CameraNvrMapping> builder)
    {
        builder.ToTable("camera_nvr_mappings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.CameraId)
            .HasColumnName("camera_id")
            .IsRequired();

        builder.Property(x => x.NvrId)
            .HasColumnName("nvr_id")
            .IsRequired();

        builder.Property(x => x.ChannelNumber)
            .HasColumnName("channel_number")
            .IsRequired();

        builder.HasOne<Device>()
            .WithMany()
            .HasForeignKey(x => x.CameraId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Device>()
            .WithMany()
            .HasForeignKey(x => x.NvrId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.NvrId, x.ChannelNumber })
            .IsUnique()
            .HasDatabaseName("ux_camera_nvr_mappings_nvr_channel");

        builder.HasIndex(x => x.CameraId)
            .HasDatabaseName("ix_camera_nvr_mappings_camera_id");
    }
}
