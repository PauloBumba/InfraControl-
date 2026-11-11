using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

/// <summary>
/// F50-T007/ADR-067 — itens do checklist genérico (tabela
/// `checklist_items`). Polimórfico: `owner_type` + `owner_id` **sem FK**
/// (dono de outro agregado — precedente `KnowledgeArticleOrigin` /
/// ADR-033); índice `(owner_type, owner_id)` no lugar do FK. Colunas
/// `Description`/`IsDone` seguem PascalCase como em `task_criteria`
/// (molde do espelho).
/// </summary>
public sealed class ChecklistItemConfiguration : IEntityTypeConfiguration<ChecklistItem>
{
    public void Configure(EntityTypeBuilder<ChecklistItem> builder)
    {
        builder.ToTable("checklist_items");

        builder.Property(x => x.OwnerType)
            .HasColumnName("owner_type")
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(x => x.OwnerId)
            .HasColumnName("owner_id");

        builder.Property(x => x.Description)
            .HasMaxLength(ChecklistItem.DescriptionMaxLength)
            .IsRequired();

        builder.Property(x => x.DoneAt)
            .HasColumnName("done_at");

        builder.HasIndex(x => new { x.OwnerType, x.OwnerId });
    }
}
