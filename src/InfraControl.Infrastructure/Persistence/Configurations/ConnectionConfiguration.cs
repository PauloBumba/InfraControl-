using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class ConnectionConfiguration : IEntityTypeConfiguration<Connection>
{
    public void Configure(EntityTypeBuilder<Connection> builder)
    {
        builder.ToTable("connections");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.SourceInterfaceId)
            .HasColumnName("source_interface_id")
            .IsRequired();

        builder.Property(x => x.TargetInterfaceId)
            .HasColumnName("target_interface_id")
            .IsRequired();

        // Backstop: exclusão de interface com conexão bloqueada no handler (409).
        builder.HasOne<NetworkInterface>()
            .WithMany()
            .HasForeignKey(x => x.SourceInterfaceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<NetworkInterface>()
            .WithMany()
            .HasForeignKey(x => x.TargetInterfaceId)
            .OnDelete(DeleteBehavior.Restrict);

        // Par DIRIGIDO único: (a,b) é diferente de (b,a).
        builder.HasIndex(x => new { x.SourceInterfaceId, x.TargetInterfaceId })
            .IsUnique();
    }
}
