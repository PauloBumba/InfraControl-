using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class TaskSlaConfiguration : IEntityTypeConfiguration<TaskSla>
{
    public void Configure(EntityTypeBuilder<TaskSla> builder)
    {
        builder.ToTable("task_slas");

        // PK = TaskId (1:1 por task) — o Id da base é ignorado.
        builder.Ignore(x => x.Id);
        builder.HasKey(x => x.TaskId);

        builder.Property(x => x.TaskId)
            .HasColumnName("task_id");

        builder.Property(x => x.SlaPolicyId)
            .HasColumnName("sla_policy_id");

        builder.Property(x => x.ResponseMinutes)
            .HasColumnName("response_minutes");

        builder.Property(x => x.ResolutionMinutes)
            .HasColumnName("resolution_minutes");

        builder.Property(x => x.ResponseDueAt)
            .HasColumnName("response_due_at");

        builder.Property(x => x.ResolutionDueAt)
            .HasColumnName("resolution_due_at");

        // Task Cascade (apagar a task limpa o SLA); SlaPolicy Restrict —
        // prazos históricos preservados (pré-check 409 + FK).
        builder.HasOne<TaskItem>()
            .WithMany()
            .HasForeignKey(x => x.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<SlaPolicy>()
            .WithMany()
            .HasForeignKey(x => x.SlaPolicyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
