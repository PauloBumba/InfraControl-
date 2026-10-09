using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

/// <summary>
/// F24-T003/K3 — snapshots de versão (tabela `knowledge_article_versions`,
/// spec docs/32 §8). Chave única `(article_id, version)`; FK do artigo
/// Cascade (apagar artigo apaga o histórico); a listagem ordena por
/// número de versão (mais recente primeiro) no repositório. `tags` =
/// coleção primitiva (JSON/`text[]`, mesmo molde do artigo).
/// </summary>
public sealed class KnowledgeArticleVersionConfiguration
    : IEntityTypeConfiguration<KnowledgeArticleVersion>
{
    public void Configure(EntityTypeBuilder<KnowledgeArticleVersion> builder)
    {
        builder.ToTable("knowledge_article_versions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.ArticleId)
            .HasColumnName("article_id");

        builder.Property(x => x.Version)
            .HasColumnName("version")
            .HasMaxLength(KnowledgeArticleVersion.VersionMaxLength)
            .IsRequired();

        builder.Property(x => x.Title)
            .HasColumnName("title")
            .HasMaxLength(KnowledgeArticle.TitleMaxLength)
            .IsRequired();

        builder.Property(x => x.Content)
            .HasColumnName("content")
            .HasMaxLength(KnowledgeArticle.ContentMaxLength)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(KnowledgeArticle.DescriptionMaxLength);

        builder.Property(x => x.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(x => x.CategoryId)
            .HasColumnName("category_id");

        builder.Property(x => x.Tags)
            .HasColumnName("tags")
            .HasField("_tags");

        builder.Property(x => x.Reason)
            .HasColumnName("reason")
            .HasMaxLength(KnowledgeArticleVersion.ReasonMaxLength)
            .IsRequired();

        builder.Property(x => x.AuthorId)
            .HasColumnName("author_id");

        builder.Property(x => x.ApprovedById)
            .HasColumnName("approved_by_id");

        builder.Property(x => x.ApprovedAt)
            .HasColumnName("approved_at");

        // Mesmo critério do artigo (K1): apagar o usuário não apaga a
        // versão — autor/aprovador viram NULL e o nome resolve null.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.AuthorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.ApprovedById)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.ArticleId, x.Version })
            .IsUnique()
            .HasDatabaseName("ux_knowledge_article_versions_article_version");

        builder.HasOne<KnowledgeArticle>()
            .WithMany()
            .HasForeignKey(x => x.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
