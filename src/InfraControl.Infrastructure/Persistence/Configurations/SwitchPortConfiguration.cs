using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class SwitchPortConfiguration : IEntityTypeConfiguration<SwitchPort>
{
    public void Configure(EntityTypeBuilder<SwitchPort> builder)
    {
        builder.ToTable("switch_ports");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.DeviceId)
            .HasColumnName("device_id")
            .IsRequired();

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(SwitchPort.NameMaxLength)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(SwitchPort.DescriptionMaxLength);

        // null = porta ainda não observada (o discovery SNMP da F4 preenche).
        builder.Property(x => x.LinkUp)
            .HasColumnName("link_up");

        // F30-T001 S4a — cadastro semi-estático preenchido por Observe
        // (controller UniFi/SNMP). Enum como STRING, como os demais da casa:
        // reordenar/incluir membros não corrói dado antigo. PoE NÃO entra aqui
        // (ADR-028 §10: potência PoE é métrica sob demanda — §7 item 8 do
        // docs/51). null = ainda não observado.
        builder.Property(x => x.Speed)
            .HasColumnName("speed")
            .HasConversion<string>();

        builder.Property(x => x.Duplex)
            .HasColumnName("duplex")
            .HasConversion<string>();

        // Backstop de integridade: switch com portas não pode ser excluído
        // (o bloqueio de UX fica no DeleteDeviceCommandHandler → 409).
        builder.HasOne<Device>()
            .WithMany()
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);

        // Índice único (device_id, LOWER(name)) via SQL bruto na migração —
        // o modelo EF não suporta índice funcional via LINQ.
    }
}
