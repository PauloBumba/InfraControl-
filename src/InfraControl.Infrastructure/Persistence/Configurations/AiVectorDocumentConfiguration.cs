using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

/// <summary>Índice vetorial do RAG (ADR-038 §4) — pgvector <c>vector(768)</c>.</summary>
public sealed class AiVectorDocumentConfiguration : IEntityTypeConfiguration<AiVectorDocument>
{
    public void Configure(EntityTypeBuilder<AiVectorDocument> builder)
    {
        builder.ToTable("ai_vector_documents");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasMaxLength(200);

        builder.Property(x => x.Text).HasColumnName("text").HasColumnType("text");

        // Sem typmod: aceita qualquer dimensão — o provider de embeddings
        // é configurável (Ai:Embeddings, ADR-038 §2). Um modelo por índice
        // (misturar dimensões/modelos corrompe a similaridade).
        builder.Property(x => x.Embedding)
            .HasColumnName("embedding")
            .HasColumnType("vector");

        builder.Property(x => x.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb");
    }
}
