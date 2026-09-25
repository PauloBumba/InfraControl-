using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class DiscoveryRunConfiguration : IEntityTypeConfiguration<DiscoveryRun>
{
    public void Configure(EntityTypeBuilder<DiscoveryRun> builder)
    {
        builder.ToTable("discovery_runs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.DeviceId)
            .HasColumnName("device_id")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(x => x.StartedAt)
            .HasColumnName("started_at")
            .IsRequired();

        builder.Property(x => x.FinishedAt)
            .HasColumnName("finished_at");

        builder.Property(x => x.ErrorMessage)
            .HasColumnName("error_message")
            .HasMaxLength(DiscoveryRun.ErrorMessageMaxLength);

        // Mesma decisão do MonitoringCheck: histórico é linha perdida quando
        // o device é excluído (FK cascade).
        builder.HasOne<Device>()
            .WithMany()
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
