using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class ProjectConclusionConfiguration
    : IEntityTypeConfiguration<ProjectConclusion>
{
    public void Configure(EntityTypeBuilder<ProjectConclusion> builder)
    {
        builder.ToTable("project_conclusions");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(ProjectConclusion.DescriptionMaxLength)
            .IsRequired();

        builder.Property(x => x.ActivitiesPerformed)
            .HasColumnName("activities_performed")
            .HasMaxLength(ProjectConclusion.ActivitiesMaxLength)
            .IsRequired();

        builder.Property(x => x.ResultsAchieved)
            .HasColumnName("results_achieved")
            .HasMaxLength(ProjectConclusion.ResultsMaxLength)
            .IsRequired();

        builder.Property(x => x.ProblemsEncountered)
            .HasColumnName("problems_encountered")
            .HasMaxLength(ProjectConclusion.ProblemsMaxLength);

        builder.Property(x => x.SolutionsImplemented)
            .HasColumnName("solutions_implemented")
            .HasMaxLength(ProjectConclusion.SolutionsMaxLength);

        builder.Property(x => x.EvidenceSummary)
            .HasColumnName("evidence_summary")
            .HasMaxLength(ProjectConclusion.EvidenceSummaryMaxLength);

        builder.Property(x => x.CompletedAt)
            .HasColumnName("completed_at")
            .IsRequired();

        builder.Property(x => x.SubmittedById)
            .HasColumnName("submitted_by_id")
            .IsRequired();

        builder.Property(x => x.DecidedById)
            .HasColumnName("decided_by_id");

        builder.Property(x => x.DecisionNotes)
            .HasColumnName("decision_notes")
            .HasMaxLength(ProjectConclusion.DecisionNotesMaxLength);

        builder.Property(x => x.DecidedAt)
            .HasColumnName("decided_at");

        builder.Property(x => x.CreateDateTime)
            .HasColumnName("create_date_time");

        builder.Property(x => x.UpdateDateTime)
            .HasColumnName("update_date_time");

        builder.HasIndex(x => x.ProjectId);
        builder.HasIndex(x => x.Status);
    }
}
