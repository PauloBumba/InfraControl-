using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class KnowledgeCategoryConfiguration
    : IEntityTypeConfiguration<KnowledgeCategory>
{
    public void Configure(EntityTypeBuilder<KnowledgeCategory> builder)
    {
        builder.ToTable("knowledge_categories");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(KnowledgeCategory.NameMaxLength)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(KnowledgeCategory.DescriptionMaxLength);

        // Índice único case-insensitive criado via SQL bruto na migração
        // (CREATE UNIQUE INDEX ... ON knowledge_categories (LOWER(name))) —
        // precedente da Location: o modelo EF não suporta índice
        // funcional via LINQ.
    }
}
