using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class TaskAssigneeConfiguration : IEntityTypeConfiguration<TaskAssignee>
{
    public void Configure(EntityTypeBuilder<TaskAssignee> builder)
    {
        builder.ToTable("task_assignees");

        // A chave é o par (TaskId, UserId) — o Id da base é ignorado.
        builder.Ignore(x => x.Id);
        builder.HasKey(x => new { x.TaskId, x.UserId });

        builder.Property(x => x.TaskId)
            .HasColumnName("task_id");
        builder.Property(x => x.UserId)
            .HasColumnName("user_id");

        // F7-T002: Cascade nas duas pontas — vínculo derivado; apagar a
        // task ou o usuário limpa os assignees (sem 409 surpresa no Delete).
        builder.HasOne<TaskItem>()
            .WithMany()
            .HasForeignKey(x => x.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
