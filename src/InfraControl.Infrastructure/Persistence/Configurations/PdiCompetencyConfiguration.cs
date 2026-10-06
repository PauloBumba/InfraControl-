using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class PdiCompetencyConfiguration
    : IEntityTypeConfiguration<PdiCompetency>
{
    public void Configure(EntityTypeBuilder<PdiCompetency> builder)
    {
        builder.ToTable("pdi_competencies");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        // Nome único case-insensitive garantido no handler (409) — o
        // índice aqui é simples para não estourar 500 em corrida.
        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(PdiCompetency.NameMaxLength)
            .IsRequired();

        builder.HasIndex(x => x.Name);

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(PdiCompetency.DescriptionMaxLength);

        builder.Property(x => x.CreateDateTime)
            .HasColumnName("create_date_time")
            .IsRequired();

        builder.Property(x => x.UpdateDateTime)
            .HasColumnName("update_date_time")
            .IsRequired();
    }
}
