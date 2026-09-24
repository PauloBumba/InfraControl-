using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("devices");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.LocationId)
            .HasColumnName("location_id")
            .IsRequired();

        builder.Property(x => x.RackId)
            .HasColumnName("rack_id");

        builder.Property(x => x.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(Device.NameMaxLength)
            .IsRequired();

        builder.Property(x => x.Ip)
            .HasColumnName("ip")
            .HasMaxLength(Device.IpMaxLength);

        builder.Property(x => x.Mac)
            .HasColumnName("mac")
            .HasMaxLength(Device.MacMaxLength);

        builder.Property(x => x.Serial)
            .HasColumnName("serial")
            .HasMaxLength(Device.SerialMaxLength);

        builder.Property(x => x.Model)
            .HasColumnName("model")
            .HasMaxLength(Device.ModelMaxLength);

        builder.Property(x => x.Vendor)
            .HasColumnName("vendor")
            .HasMaxLength(Device.VendorMaxLength);

        builder.Property(x => x.VlanId)
            .HasColumnName("vlan_id");

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(Device.DescriptionMaxLength);

        // Backstop de integridade: local/rack com devices não pode ser excluído
        // (o bloqueio de UX fica nos handlers Delete → 409).
        builder.HasOne<Location>()
            .WithMany()
            .HasForeignKey(x => x.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Rack>()
            .WithMany()
            .HasForeignKey(x => x.RackId)
            .OnDelete(DeleteBehavior.Restrict);

        // Índice único global LOWER(name) via SQL bruto na migração.
        builder.HasIndex(x => x.LocationId);
        builder.HasIndex(x => x.RackId);
        builder.HasIndex(x => x.Type);
    }
}
