using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class MacAddressConfiguration : IEntityTypeConfiguration<MacAddress>
{
    public void Configure(EntityTypeBuilder<MacAddress> builder)
    {
        builder.ToTable("mac_addresses");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.InterfaceId)
            .HasColumnName("interface_id")
            .IsRequired();

        builder.Property(x => x.Mac)
            .HasColumnName("mac")
            .HasMaxLength(MacAddress.MacMaxLength)
            .IsRequired();

        // Backstop de integridade: interface com MACs não pode ser excluída
        // (o bloqueio de UX fica no DeleteNetworkInterfaceCommandHandler → 409).
        builder.HasOne<NetworkInterface>()
            .WithMany()
            .HasForeignKey(x => x.InterfaceId)
            .OnDelete(DeleteBehavior.Restrict);

        // Índice único global (LOWER(mac)) via SQL bruto na migração —
        // o modelo EF não suporta índice funcional via LINQ.
    }
}
