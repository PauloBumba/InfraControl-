using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class NetworkInterfaceConfiguration : IEntityTypeConfiguration<NetworkInterface>
{
    public void Configure(EntityTypeBuilder<NetworkInterface> builder)
    {
        builder.ToTable("network_interfaces");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.DeviceId)
            .HasColumnName("device_id")
            .IsRequired();

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(NetworkInterface.NameMaxLength)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(NetworkInterface.DescriptionMaxLength);

        // Backstop de integridade: device com interfaces não pode ser excluído
        // (o bloqueio de UX fica no DeleteDeviceCommandHandler → 409).
        builder.HasOne<Device>()
            .WithMany()
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);

        // Índice único (device_id, LOWER(name)) via SQL bruto na migração —
        // o modelo EF não suporta índice funcional via LINQ.
    }
}
