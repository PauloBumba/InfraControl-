using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class ProjectPhaseConfiguration : IEntityTypeConfiguration<ProjectPhase>
{
    public void Configure(EntityTypeBuilder<ProjectPhase> builder)
    {
        builder.ToTable("project_phases");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(x => x.Title)
            .HasColumnName("title")
            .HasMaxLength(ProjectPhase.TitleMaxLength)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(ProjectPhase.DescriptionMaxLength);

        builder.Property(x => x.Order)
            .HasColumnName("sort_order")
            .IsRequired();

        builder.Property(x => x.ResponsibleId)
            .HasColumnName("responsible_id");

        builder.Property(x => x.PlannedStartDate)
            .HasColumnName("planned_start_date")
            .HasColumnType("date");

        builder.Property(x => x.PlannedEndDate)
            .HasColumnName("planned_end_date")
            .HasColumnType("date");

        builder.Property(x => x.ActualStartDate)
            .HasColumnName("actual_start_date")
            .HasColumnType("date");

        builder.Property(x => x.ActualEndDate)
            .HasColumnName("actual_end_date")
            .HasColumnType("date");

        builder.Property(x => x.CompletionCriteria)
            .HasColumnName("completion_criteria")
            .HasMaxLength(ProjectPhase.CriteriaMaxLength);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(30)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(x => x.Percent)
            .HasColumnName("percent")
            .IsRequired();

        builder.Property(x => x.CreateDateTime)
            .HasColumnName("create_date_time");

        builder.Property(x => x.UpdateDateTime)
            .HasColumnName("update_date_time");

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.ProjectId, x.Order });
        builder.HasIndex(x => x.Status);
    }
}
