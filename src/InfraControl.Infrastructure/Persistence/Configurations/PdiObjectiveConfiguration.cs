using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class PdiObjectiveConfiguration
    : IEntityTypeConfiguration<PdiObjective>
{
    public void Configure(EntityTypeBuilder<PdiObjective> builder)
    {
        builder.ToTable("pdi_objectives");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        // user_id é o dono — aponta para o plano via user_id (1:1,
        // principal alternativo), mesmo vínculo semântico da timeline
        // (ADR-029 §4) mas com FK para manter a árvore íntegra.
        builder.HasOne<IndividualDevelopmentPlan>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .HasPrincipalKey(plan => plan.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.HasIndex(x => new { x.UserId, x.Order });

        builder.Property(x => x.Title)
            .HasColumnName("title")
            .HasMaxLength(PdiObjective.TitleMaxLength)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(PdiObjective.DescriptionMaxLength);

        builder.Property(x => x.CompetencyId)
            .HasColumnName("competency_id");

        builder.HasOne<PdiCompetency>()
            .WithMany()
            .HasForeignKey(x => x.CompetencyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.CurrentSituation)
            .HasColumnName("current_situation")
            .HasMaxLength(PdiObjective.SituationMaxLength);

        builder.Property(x => x.DesiredSituation)
            .HasColumnName("desired_situation")
            .HasMaxLength(PdiObjective.SituationMaxLength);

        builder.Property(x => x.ExpectedResult)
            .HasColumnName("expected_result")
            .HasMaxLength(PdiObjective.SituationMaxLength);

        builder.Property(x => x.Indicator)
            .HasColumnName("indicator")
            .HasMaxLength(PdiObjective.TitleMaxLength);

        builder.Property(x => x.TargetDate)
            .HasColumnName("target_date")
            .HasColumnType("date");

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
