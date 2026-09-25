using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class IpAddressConfiguration : IEntityTypeConfiguration<IpAddress>
{
    public void Configure(EntityTypeBuilder<IpAddress> builder)
    {
        builder.ToTable("ip_addresses");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.InterfaceId)
            .HasColumnName("interface_id")
            .IsRequired();

        builder.Property(x => x.Ip)
            .HasColumnName("ip")
            .HasMaxLength(IpAddress.IpMaxLength)
            .IsRequired();

        // Unicidade global: a normalização canônica do domínio (IPv4 pontilhado,
        // IPv6 minúsculo com ::) torna o índice UNIQUE simples expressável em LINQ.
        builder.HasIndex(x => x.Ip)
            .IsUnique();

        // Backstop de integridade: interface com IPs não pode ser excluída
        // (o bloqueio de UX fica no DeleteNetworkInterfaceCommandHandler → 409).
        builder.HasOne<NetworkInterface>()
            .WithMany()
            .HasForeignKey(x => x.InterfaceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
