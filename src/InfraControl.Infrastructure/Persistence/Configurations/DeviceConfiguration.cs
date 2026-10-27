using System.Linq;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

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

        builder.Property(x => x.ImagePath)
            .HasColumnName("image_path")
            .HasMaxLength(Device.ImagePathMaxLength);

        // F30-T006c / docs/47 §A: cadastro SIP do telefone (extensão e PBX).
        builder.Property(x => x.Extension)
            .HasColumnName("extension")
            .HasMaxLength(Device.ExtensionMaxLength);

        builder.Property(x => x.SipServer)
            .HasColumnName("sip_server")
            .HasMaxLength(Device.SipServerMaxLength);

        // F34-T006 (ADR-060): SO informado — string (precedente Type) e
        // nullable (null = não informado).
        builder.Property(x => x.OperatingSystem)
            .HasColumnName("operating_system")
            .HasConversion<string>()
            .HasMaxLength(20);

        // F31-T002 / docs/53 D4: vínculos da VM (FK self — 1 hypervisor e
        // 1 vCenter por VM; backstop Restrict + 409 no handler de exclusão).
        builder.Property(x => x.HypervisorDeviceId)
            .HasColumnName("hypervisor_device_id");

        builder.Property(x => x.VCenterDeviceId)
            .HasColumnName("vcenter_device_id");

        // F2-T008 / ADR-014: checks do device como lista CSV ("Ping,SnmpToner").
        builder.Property(x => x.ProfileIsCustom)
            .HasColumnName("profile_is_custom")
            .IsRequired();

        var checksConverter = new ValueConverter<List<CheckKind>, string>(
            v => string.Join(",", v.Select(check => check.ToString())),
            v => string.IsNullOrWhiteSpace(v)
                ? new List<CheckKind>()
                : v.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => Enum.Parse<CheckKind>(s.Trim()))
                    .ToList());

        var checksComparer = new ValueComparer<List<CheckKind>>(
            (a, b) => a != null && b != null && a.SequenceEqual(b),
            v => v.Aggregate(0, (hash, check) => HashCode.Combine(hash, check.GetHashCode())),
            v => v.ToList());

        builder.Property("_monitoringChecks")
            .HasColumnName("monitoring_checks")
            .HasMaxLength(500)
            .HasConversion(checksConverter)
            .Metadata.SetValueComparer(checksComparer);

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

        builder.HasOne<Device>()
            .WithMany()
            .HasForeignKey(x => x.HypervisorDeviceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Device>()
            .WithMany()
            .HasForeignKey(x => x.VCenterDeviceId)
            .OnDelete(DeleteBehavior.Restrict);

        // Índice único global LOWER(name) via SQL bruto na migração.
        builder.HasIndex(x => x.LocationId);
        builder.HasIndex(x => x.RackId);
        builder.HasIndex(x => x.Type);
        builder.HasIndex(x => x.HypervisorDeviceId);
        builder.HasIndex(x => x.VCenterDeviceId);
    }
}
