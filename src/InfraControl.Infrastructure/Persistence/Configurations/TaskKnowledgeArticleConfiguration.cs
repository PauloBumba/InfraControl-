using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class TaskKnowledgeArticleConfiguration
    : IEntityTypeConfiguration<TaskKnowledgeArticle>
{
    public void Configure(EntityTypeBuilder<TaskKnowledgeArticle> builder)
    {
        builder.ToTable("task_knowledge_articles");

        // A chave é o par (TaskId, ArticleId) — o Id da base é ignorado.
        builder.Ignore(x => x.Id);
        builder.HasKey(x => new { x.TaskId, x.ArticleId });

        builder.Property(x => x.TaskId)
            .HasColumnName("task_id");
        builder.Property(x => x.ArticleId)
            .HasColumnName("article_id");

        // F23-T005/P5a: Task Cascade (apagar a task limpa os vínculos);
        // Article Cascade — exclusão física de artigo (ADR-030 §5) também
        // limpa, mesmo comportamento de knowledge_article_incidents.
        builder.HasOne<TaskItem>()
            .WithMany()
            .HasForeignKey(x => x.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<KnowledgeArticle>()
            .WithMany()
            .HasForeignKey(x => x.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
