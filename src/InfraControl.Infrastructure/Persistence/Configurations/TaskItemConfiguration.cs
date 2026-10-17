using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

/// <summary>F7-T001: TaskItem — título/descrição limitados, índices de estado/tipo.</summary>
public sealed class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("tasks");

        builder.Property(x => x.Title)
            .HasMaxLength(TaskItem.TitleMaxLength)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(TaskItem.DescriptionMaxLength);

        builder.Property(x => x.Kind)
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(x => x.State)
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();

        // F23-T001 — identificação básica: prioridade (padrão Medium na
        // migração), prazo (date, precedente target_date do PDI) e
        // responsável sem FK (precedente manager_id, ADR-029 — o usuário
        // pode ser excluído e a task permanece).
        builder.Property(x => x.Priority)
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(x => x.DueDate)
            .HasColumnType("date");

        builder.Property(x => x.ResponsibleId);

        // F32-T001 — criador da task (espelho CreatedById do Project):
        // sem FK (mesmo critério do ResponsibleId / ADR-029); nulo só em
        // tasks legadas anteriores à migração AddTaskCreatedById.
        builder.Property(x => x.CreatedById);

        // F23-T004/P4a — subtarefas: auto-relação na própria `tasks`;
        // pai excluído → filho sobrevive (SetNull) e vira task raiz.
        builder.HasOne<TaskItem>()
            .WithMany()
            .HasForeignKey(x => x.ParentTaskId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.State);
        builder.HasIndex(x => x.Kind);
        builder.HasIndex(x => x.Priority);
        builder.HasIndex(x => x.ParentTaskId);
    }
}
