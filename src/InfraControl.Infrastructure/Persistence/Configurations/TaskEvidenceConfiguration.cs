using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class TaskEvidenceConfiguration : IEntityTypeConfiguration<TaskEvidence>
{
    public void Configure(EntityTypeBuilder<TaskEvidence> builder)
    {
        builder.ToTable("task_evidences");

        builder.Property(x => x.TaskId)
            .HasColumnName("task_id");
        builder.Property(x => x.FileName)
            .HasMaxLength(TaskEvidence.FileNameMaxLength)
            .IsRequired();
        // Nome em disco (GUID + extensão) — metadado, nunca caminho (ADR-033).
        builder.Property(x => x.StoredName)
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(x => x.ContentType)
            .HasMaxLength(TaskEvidence.ContentTypeMaxLength)
            .IsRequired();
        builder.Property(x => x.Note)
            .HasMaxLength(TaskEvidence.NoteMaxLength);

        // F23-T004/P4c: Task Cascade (apagar a task apaga as evidências;
        // os arquivos em disco ficam — reprocesso de exclusão em massa
        // fora do escopo por enquanto, ADR-033).
        builder.HasOne<TaskItem>()
            .WithMany()
            .HasForeignKey(x => x.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.TaskId);
    }
}
