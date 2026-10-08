using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class TaskImpedimentConfiguration
    : IEntityTypeConfiguration<TaskImpediment>
{
    public void Configure(EntityTypeBuilder<TaskImpediment> builder)
    {
        builder.ToTable("task_impediments");

        builder.Property(x => x.TaskId)
            .HasColumnName("task_id");
        builder.Property(x => x.Description)
            .HasMaxLength(TaskImpediment.DescriptionMaxLength)
            .IsRequired();
        builder.Property(x => x.ResolvedAt)
            .HasColumnName("resolved_at");

        // F23-T004/P4b: Task Cascade (apagar a task apaga os impedimentos)
        // — mesmo molde dos critérios.
        builder.HasOne<TaskItem>()
            .WithMany()
            .HasForeignKey(x => x.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.TaskId);
    }
}
