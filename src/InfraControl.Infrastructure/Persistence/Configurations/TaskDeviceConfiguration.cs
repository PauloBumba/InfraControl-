using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class TaskDeviceConfiguration : IEntityTypeConfiguration<TaskDevice>
{
    public void Configure(EntityTypeBuilder<TaskDevice> builder)
    {
        builder.ToTable("task_devices");

        // A chave é o par (TaskId, DeviceId) — o Id da base é ignorado.
        builder.Ignore(x => x.Id);
        builder.HasKey(x => new { x.TaskId, x.DeviceId });

        builder.Property(x => x.TaskId)
            .HasColumnName("task_id");
        builder.Property(x => x.DeviceId)
            .HasColumnName("device_id");

        // F7-T003: Task Cascade (apagar a task limpa os vínculos); Device
        // Restrict — excluir device com tasks vinculadas é bloqueado no
        // DeleteDeviceCommand (pré-check + FK backstop, padrão Ports).
        builder.HasOne<TaskItem>()
            .WithMany()
            .HasForeignKey(x => x.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Device>()
            .WithMany()
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
