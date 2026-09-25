using InfraControl.Domain.Entities;
using InfraControl.Domain.RemoteAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class RemoteSessionConfiguration : IEntityTypeConfiguration<RemoteSession>
{
    public void Configure(EntityTypeBuilder<RemoteSession> builder)
    {
        builder.ToTable("remote_sessions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.DeviceId)
            .HasColumnName("device_id")
            .IsRequired();

        builder.Property(x => x.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(x => x.Protocol)
            .HasColumnName("protocol")
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(x => x.FailureReason)
            .HasColumnName("failure_reason")
            .HasMaxLength(RemoteSession.FailureReasonMaxLength);

        builder.Property(x => x.StartedAt)
            .HasColumnName("started_at")
            .IsRequired();

        builder.Property(x => x.EndedAt)
            .HasColumnName("ended_at");

        builder.Property(x => x.CreateDateTime)
            .HasColumnName("create_datetime");

        builder.Property(x => x.UpdateDateTime)
            .HasColumnName("update_datetime");

        builder.HasIndex(x => new { x.DeviceId, x.StartedAt })
            .HasDatabaseName("IX_remote_sessions_device_id_started_at");

        // Registro auditável da sessão: exclusão do device apaga as sessões
        // junto (mesmo comportamento do histórico de monitoramento).
        builder.HasOne<Device>()
            .WithMany()
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
