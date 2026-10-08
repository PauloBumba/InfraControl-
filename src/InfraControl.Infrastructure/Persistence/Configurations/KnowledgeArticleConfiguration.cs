using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class KnowledgeArticleConfiguration
    : IEntityTypeConfiguration<KnowledgeArticle>
{
    public void Configure(EntityTypeBuilder<KnowledgeArticle> builder)
    {
        builder.ToTable("knowledge_articles");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.Title)
            .HasColumnName("title")
            .HasMaxLength(KnowledgeArticle.TitleMaxLength)
            .IsRequired();

        builder.Property(x => x.Content)
            .HasColumnName("content")
            .HasMaxLength(KnowledgeArticle.ContentMaxLength)
            .IsRequired();

        builder.Property(x => x.CategoryId)
            .HasColumnName("category_id");

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32);

        // F24-T001/K1 — identificação (docs/32 §5–§6). Type nullable:
        // artigos legados (anteriores ao K1) ficam sem tipo até a
        // primeira edição (o Update exige).
        builder.Property(x => x.Type)
            .HasColumnName("type")
            .HasMaxLength(50);

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(KnowledgeArticle.DescriptionMaxLength);

        builder.Property(x => x.AuthorId)
            .HasColumnName("author_id");

        builder.Property(x => x.ResponsibleId)
            .HasColumnName("responsible_id");

        // FK SetNull (decisão F10-T002): apagar a categoria não apaga nem
        // bloqueia o artigo — o conhecimento sobrevive à reorganização das
        // categorias (T001 não tinha FK; a constraint vem nesta migration).
        builder.HasOne<KnowledgeCategory>()
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
