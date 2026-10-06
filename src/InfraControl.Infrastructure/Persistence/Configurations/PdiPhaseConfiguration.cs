using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class PdiPhaseConfiguration
    : IEntityTypeConfiguration<PdiPhase>
{
    public void Configure(EntityTypeBuilder<PdiPhase> builder)
    {
        builder.ToTable("pdi_phases");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.HasOne<PdiStrategy>()
            .WithMany()
            .HasForeignKey(x => x.StrategyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.StrategyId)
            .HasColumnName("strategy_id")
            .IsRequired();

        builder.HasIndex(x => new { x.StrategyId, x.Order });

        builder.Property(x => x.Title)
            .HasColumnName("title")
            .HasMaxLength(PdiPhase.TitleMaxLength)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(PdiPhase.DescriptionMaxLength);

        // Responsável (§7) — usuário livre (dono/gestor/terceiro); sem
        // FK, mesmo critério de ActorId na timeline (ADR-029 §4).
        builder.Property(x => x.ResponsibleId)
            .HasColumnName("responsible_id");

        builder.Property(x => x.StartDate)
            .HasColumnName("start_date")
            .HasColumnType("date");

        builder.Property(x => x.EndDate)
            .HasColumnName("end_date")
            .HasColumnType("date");

        builder.Property(x => x.CompletionCriteria)
            .HasColumnName("completion_criteria")
            .HasMaxLength(PdiPhase.CriteriaMaxLength);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Percent)
            .HasColumnName("percent")
            .IsRequired();

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
