using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class TaskCriterionConfiguration : IEntityTypeConfiguration<TaskCriterion>
{
    public void Configure(EntityTypeBuilder<TaskCriterion> builder)
    {
        builder.ToTable("task_criteria");

        builder.Property(x => x.TaskId)
            .HasColumnName("task_id");
        builder.Property(x => x.Description)
            .HasMaxLength(TaskCriterion.DescriptionMaxLength)
            .IsRequired();
        builder.Property(x => x.DoneAt)
            .HasColumnName("done_at");

        // F23-T003: Task Cascade (apagar a task apaga os critérios) —
        // mesmo molde dos comentários.
        builder.HasOne<TaskItem>()
            .WithMany()
            .HasForeignKey(x => x.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.TaskId);
    }
}
