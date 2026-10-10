using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.Code)
            .HasColumnName("code")
            .HasMaxLength(Project.CodeMaxLength)
            .IsRequired();

        builder.HasIndex(x => x.Code)
            .IsUnique();

        builder.Property(x => x.Title)
            .HasColumnName("title")
            .HasMaxLength(Project.TitleMaxLength)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(Project.DescriptionMaxLength)
            .IsRequired();

        builder.Property(x => x.Justification)
            .HasColumnName("justification")
            .HasMaxLength(Project.JustificationMaxLength);

        builder.Property(x => x.Priority)
            .HasColumnName("priority")
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(30)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(x => x.LeaderId)
            .HasColumnName("leader_id")
            .IsRequired();

        builder.Property(x => x.ManagerId)
            .HasColumnName("manager_id");

        builder.Property(x => x.AdminApproverId)
            .HasColumnName("admin_approver_id");

        builder.Property(x => x.ApprovalNotes)
            .HasColumnName("approval_notes")
            .HasMaxLength(Project.ApprovalNotesMaxLength);

        builder.Property(x => x.ApprovedAt)
            .HasColumnName("approved_at");

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

        builder.Property(x => x.CreateDateTime)
            .HasColumnName("create_date_time");

        builder.Property(x => x.UpdateDateTime)
            .HasColumnName("update_date_time");

        builder.HasMany(x => x.Members)
            .WithOne()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.Priority);
        builder.HasIndex(x => x.LeaderId);
    }
}
