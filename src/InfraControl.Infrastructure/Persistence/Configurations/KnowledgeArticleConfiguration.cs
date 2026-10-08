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

        // FK SetNull (decisão F10-T002): apagar a categoria não apaga nem
        // bloqueia o artigo — o conhecimento sobrevive à reorganização das
        // categorias (T001 não tinha FK; a constraint vem nesta migration).
        builder.HasOne<KnowledgeCategory>()
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        // F24-T001/K1 — identificação (spec docs/32 §5–§6): tipo (enum →
        // string), descrição, autor e responsável. user_id/refs: SetNull
        // como a categoria — apagar o usuário não apaga nem bloqueia o
        // artigo (o produto tem exclusão dura de usuário; Restrict do
        // TaskComment bloquearia a exclusão; a autoria histórica fica na
        // auditoria e o nome sai null no DTO).
        builder.Property(x => x.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(KnowledgeArticle.DescriptionMaxLength);

        builder.Property(x => x.AuthorId)
            .HasColumnName("author_id");

        builder.Property(x => x.ResponsibleId)
            .HasColumnName("responsible_id");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.AuthorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.ResponsibleId)
            .OnDelete(DeleteBehavior.SetNull);

        // Tags: coleção primitiva (EF → coluna JSON `tags`); o backing
        // field é explícito porque a propriedade é só-leitura. Linhas
        // antigas NULL → getter devolve []. Filtro do K7 usa Contains
        // (traduzido para json_each/jsonb nos dois providers).
        builder.Property(x => x.Tags)
            .HasColumnName("tags")
            .HasField("_tags");
    }
}
