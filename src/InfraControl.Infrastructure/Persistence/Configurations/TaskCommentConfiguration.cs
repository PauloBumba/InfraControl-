using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class TaskCommentConfiguration : IEntityTypeConfiguration<TaskComment>
{
    public void Configure(EntityTypeBuilder<TaskComment> builder)
    {
        builder.ToTable("task_comments");

        builder.Property(x => x.TaskId)
            .HasColumnName("task_id");
        builder.Property(x => x.AuthorId)
            .HasColumnName("author_id");
        builder.Property(x => x.Content)
            .HasMaxLength(TaskComment.ContentMaxLength)
            .IsRequired();

        // F7-T005: Task Cascade (apagar a task apaga os comentários);
        // Author Restrict — preserva a autoria (produto não tem exclusão
        // de usuário, a FK nunca dispara).
        builder.HasOne<TaskItem>()
            .WithMany()
            .HasForeignKey(x => x.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.TaskId);
    }
}
