using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class MonitoringCheckConfiguration : IEntityTypeConfiguration<MonitoringCheck>
{
    public void Configure(EntityTypeBuilder<MonitoringCheck> builder)
    {
        builder.ToTable("monitoring_checks");

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

        builder.Property(x => x.Enabled)
            .HasColumnName("enabled")
            .IsRequired();

        // Device excluído → checks excluídos junto (F5-T001); o DeleteDevice
        // continua bloqueado por interfaces (FK Restrict em NetworkInterface).
        builder.HasOne<Device>()
            .WithMany()
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Um tipo de check por device.
        builder.HasIndex(x => new { x.DeviceId, x.Kind })
            .IsUnique()
            .HasDatabaseName("IX_monitoring_checks_device_id_kind");
    }
}
