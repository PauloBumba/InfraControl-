using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class KnowledgeArticleServiceConfiguration
    : IEntityTypeConfiguration<KnowledgeArticleService>
{
    public void Configure(EntityTypeBuilder<KnowledgeArticleService> builder)
    {
        builder.ToTable("knowledge_article_services");

        // A chave é o par (ArticleId, ServiceId) — o Id da base é ignorado.
        builder.Ignore(x => x.Id);
        builder.HasKey(x => new { x.ArticleId, x.ServiceId });

        builder.Property(x => x.ArticleId)
            .HasColumnName("article_id");
        builder.Property(x => x.ServiceId)
            .HasColumnName("service_id");

        // F10-T004: Article Cascade (apagar o artigo limpa os vínculos);
        // Service Cascade — decisão do usuário (30/09/2026): apagar o
        // serviço remove SÓ o vínculo, o artigo permanece (nada de
        // pré-check em DeleteServiceCommand).
        builder.HasOne<KnowledgeArticle>()
            .WithMany()
            .HasForeignKey(x => x.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Service>()
            .WithMany()
            .HasForeignKey(x => x.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
