using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class TaskPdiConfiguration : IEntityTypeConfiguration<TaskPdi>
{
    public void Configure(EntityTypeBuilder<TaskPdi> builder)
    {
        builder.ToTable("task_pdis");

        // A chave é o par (TaskId, UserId) — o Id da base é ignorado.
        builder.Ignore(x => x.Id);
        builder.HasKey(x => new { x.TaskId, x.UserId });

        builder.Property(x => x.TaskId)
            .HasColumnName("task_id");
        builder.Property(x => x.UserId)
            .HasColumnName("user_id");

        // F23-T005/P5b: Task Cascade (apagar a task limpa os vínculos).
        // user_id SEM FK — critério do ADR-029 para refs de usuário (o
        // próprio plano não tem FK em user_id); a existência do PDI é
        // validada no handler (404) e a coluna é a chave do plano no
        // produto (1 plano por usuário, /pdi/{userId}).
        builder.HasOne<TaskItem>()
            .WithMany()
            .HasForeignKey(x => x.TaskId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
