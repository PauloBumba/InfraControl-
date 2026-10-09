using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

/// <summary>
/// F24-T004/K4 — origens do artigo (tabela `knowledge_article_origins`,
/// spec docs/32 §9). Polimórfica: `origin_type` + `origin_id` sem FK
/// (alvo de outro agregado — precedente ADR-033); FK do artigo Cascade
/// (apagar artigo apaga as origens); chave única
/// `(article_id, origin_type, origin_id)` (a origem é imutável — o
/// histórico fica nas versões/K3).
/// </summary>
public sealed class KnowledgeArticleOriginConfiguration
    : IEntityTypeConfiguration<KnowledgeArticleOrigin>
{
    public void Configure(EntityTypeBuilder<KnowledgeArticleOrigin> builder)
    {
        builder.ToTable("knowledge_article_origins");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.ArticleId)
            .HasColumnName("article_id");

        builder.Property(x => x.OriginType)
            .HasColumnName("origin_type")
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(x => x.OriginId)
            .HasColumnName("origin_id");

        builder.HasIndex(x => new { x.ArticleId, x.OriginType, x.OriginId })
            .IsUnique()
            .HasDatabaseName("ux_knowledge_article_origins_article_origin");

        builder.HasOne<KnowledgeArticle>()
            .WithMany()
            .HasForeignKey(x => x.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
