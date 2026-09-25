using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class VlanConfiguration : IEntityTypeConfiguration<Vlan>
{
    public void Configure(EntityTypeBuilder<Vlan> builder)
    {
        builder.ToTable("vlans");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(Vlan.NameMaxLength)
            .IsRequired();

        builder.Property(x => x.VlanNumber)
            .HasColumnName("vlan_number")
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(Vlan.DescriptionMaxLength);

        // Tag 802.1Q única global — expressível via LINQ (como IX_ip_addresses_ip).
        builder.HasIndex(x => x.VlanNumber)
            .IsUnique();

        // Índice único case-insensitive do nome é criado via SQL bruto na migração
        // (CREATE UNIQUE INDEX ... ON vlans (LOWER(name))) — o modelo EF não
        // suporta índice funcional via LINQ.
    }
}
