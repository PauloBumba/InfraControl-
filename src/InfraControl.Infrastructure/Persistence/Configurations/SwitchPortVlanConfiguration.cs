using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class SwitchPortVlanConfiguration : IEntityTypeConfiguration<SwitchPortVlan>
{
    public void Configure(EntityTypeBuilder<SwitchPortVlan> builder)
    {
        builder.ToTable("switch_port_vlans");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.SwitchPortId)
            .HasColumnName("switch_port_id")
            .IsRequired();

        builder.Property(x => x.VlanId)
            .HasColumnName("vlan_id")
            .IsRequired();

        // Backstop: exclusão de porta/VLAN com vínculo bloqueada no handler (409).
        builder.HasOne<SwitchPort>()
            .WithMany()
            .HasForeignKey(x => x.SwitchPortId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Vlan>()
            .WithMany()
            .HasForeignKey(x => x.VlanId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unicidade natural da junção (par de GUIDs — sem case, LINQ resolve).
        builder.HasIndex(x => new { x.SwitchPortId, x.VlanId })
            .IsUnique();
    }
}
