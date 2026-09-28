using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class TaskHistoryConfiguration : IEntityTypeConfiguration<TaskHistory>
{
    public void Configure(EntityTypeBuilder<TaskHistory> builder)
    {
        builder.ToTable("task_histories");

        builder.Property(x => x.TaskId)
            .HasColumnName("task_id");
        builder.Property(x => x.ActorId)
            .HasColumnName("actor_id");
        builder.Property(x => x.Action)
            .HasMaxLength(40)
            .HasConversion<string>()
            .IsRequired();
        builder.Property(x => x.Detail)
            .HasMaxLength(TaskHistory.DetailMaxLength);

        // F7-T006: Task Cascade (histórico morre com a task); Actor Restrict
        // não existe — actor_id é nullable (sistema/autor desconhecido) e o
        // produto não exclui usuários; sem FK é mais simples para auditoria.
        builder.HasOne<TaskItem>()
            .WithMany()
            .HasForeignKey(x => x.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.TaskId);
    }
}
