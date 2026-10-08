using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class TaskPhaseConfiguration : IEntityTypeConfiguration<TaskPhase>
{
    public void Configure(EntityTypeBuilder<TaskPhase> builder)
    {
        builder.ToTable("task_phases");

        builder.Property(x => x.TaskId)
            .HasColumnName("task_id");
        builder.Property(x => x.Name)
            .HasMaxLength(TaskPhase.NameMaxLength)
            .IsRequired();
        builder.Property(x => x.Position)
            .HasColumnName("position");
        builder.Property(x => x.IsCurrent)
            .HasColumnName("is_current");
        builder.Property(x => x.CompletedAt)
            .HasColumnName("completed_at");

        // F23-T004/P4d: Task Cascade (apagar a task apaga as fases) —
        // mesmo molde dos critérios.
        builder.HasOne<TaskItem>()
            .WithMany()
            .HasForeignKey(x => x.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ordem de leitura dentro da task (gaps aceitos após exclusão).
        builder.HasIndex(x => new { x.TaskId, x.Position });
    }
}
