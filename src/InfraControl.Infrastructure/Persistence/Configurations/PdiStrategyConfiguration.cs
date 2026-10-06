using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class PdiStrategyConfiguration
    : IEntityTypeConfiguration<PdiStrategy>
{
    public void Configure(EntityTypeBuilder<PdiStrategy> builder)
    {
        builder.ToTable("pdi_strategies");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.HasOne<PdiObjective>()
            .WithMany()
            .HasForeignKey(x => x.ObjectiveId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.ObjectiveId)
            .HasColumnName("objective_id")
            .IsRequired();

        builder.HasIndex(x => new { x.ObjectiveId, x.Order });

        builder.Property(x => x.Title)
            .HasColumnName("title")
            .HasMaxLength(PdiStrategy.TitleMaxLength)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(PdiStrategy.DescriptionMaxLength);

        builder.Property(x => x.Order)
            .HasColumnName("sort_order")
            .IsRequired();

        builder.Property(x => x.CreateDateTime)
            .HasColumnName("create_date_time")
            .IsRequired();

        builder.Property(x => x.UpdateDateTime)
            .HasColumnName("update_date_time")
            .IsRequired();
    }
}
