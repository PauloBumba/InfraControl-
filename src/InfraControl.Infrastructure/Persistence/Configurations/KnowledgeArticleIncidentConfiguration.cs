using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class KnowledgeArticleIncidentConfiguration
    : IEntityTypeConfiguration<KnowledgeArticleIncident>
{
    public void Configure(EntityTypeBuilder<KnowledgeArticleIncident> builder)
    {
        builder.ToTable("knowledge_article_incidents");

        // A chave é o par (ArticleId, IncidentId) — o Id da base é ignorado.
        builder.Ignore(x => x.Id);
        builder.HasKey(x => new { x.ArticleId, x.IncidentId });

        builder.Property(x => x.ArticleId)
            .HasColumnName("article_id");
        builder.Property(x => x.IncidentId)
            .HasColumnName("incident_id");

        // F10-T004: Article Cascade (apagar o artigo limpa os vínculos);
        // Incident Cascade — decisão do usuário (30/09/2026): vínculo de KB
        // é metadado descartável (não há exclusão de incidente no produto,
        // a FK é o comportamento se um dia houver).
        builder.HasOne<KnowledgeArticle>()
            .WithMany()
            .HasForeignKey(x => x.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Incident>()
            .WithMany()
            .HasForeignKey(x => x.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
