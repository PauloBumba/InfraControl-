using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class KnowledgeArticleDeviceConfiguration
    : IEntityTypeConfiguration<KnowledgeArticleDevice>
{
    public void Configure(EntityTypeBuilder<KnowledgeArticleDevice> builder)
    {
        builder.ToTable("knowledge_article_devices");

        // A chave é o par (ArticleId, DeviceId) — o Id da base é ignorado.
        builder.Ignore(x => x.Id);
        builder.HasKey(x => new { x.ArticleId, x.DeviceId });

        builder.Property(x => x.ArticleId)
            .HasColumnName("article_id");
        builder.Property(x => x.DeviceId)
            .HasColumnName("device_id");

        // F10-T004: Article Cascade (apagar o artigo limpa os vínculos);
        // Device Cascade — decisão do usuário (30/09/2026): apagar o
        // device remove SÓ o vínculo, o artigo permanece (vínculo de KB é
        // metadado descartável — nada de pré-check em DeleteDeviceCommand).
        builder.HasOne<KnowledgeArticle>()
            .WithMany()
            .HasForeignKey(x => x.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Device>()
            .WithMany()
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
