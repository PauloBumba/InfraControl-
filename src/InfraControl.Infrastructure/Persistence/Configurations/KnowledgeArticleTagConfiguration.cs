using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class KnowledgeArticleTagConfiguration
    : IEntityTypeConfiguration<KnowledgeArticleTag>
{
    public void Configure(EntityTypeBuilder<KnowledgeArticleTag> builder)
    {
        builder.ToTable("knowledge_article_tags");

        // A chave é o par (ArticleId, Tag) — o Id da base é ignorado
        // (mesmo precedente de knowledge_article_devices, F10-T004).
        builder.Ignore(x => x.Id);
        builder.HasKey(x => new { x.ArticleId, x.Tag });

        builder.Property(x => x.ArticleId)
            .HasColumnName("article_id");

        builder.Property(x => x.Tag)
            .HasColumnName("tag")
            .HasMaxLength(KnowledgeArticle.TagMaxLength);

        // K1: apagar o artigo limpa as tags (parte do agregado).
        builder.HasOne<KnowledgeArticle>()
            .WithMany(x => x.Tags)
            .HasForeignKey(x => x.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
