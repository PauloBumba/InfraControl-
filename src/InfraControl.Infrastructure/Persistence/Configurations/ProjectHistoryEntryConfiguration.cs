using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class ProjectHistoryEntryConfiguration : IEntityTypeConfiguration<ProjectHistoryEntry>
{
    public void Configure(EntityTypeBuilder<ProjectHistoryEntry> builder)
    {
        builder.ToTable("project_history_entries");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(x => x.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.ActorId)
            .HasColumnName("actor_id")
            .IsRequired();

        builder.Property(x => x.Detail)
            .HasColumnName("detail")
            .HasMaxLength(ProjectHistoryEntry.MaxDetailLength);

        builder.Property(x => x.Reason)
            .HasColumnName("reason")
            .HasMaxLength(ProjectHistoryEntry.MaxReasonLength);

        builder.Property(x => x.CreateDateTime)
            .HasColumnName("create_date_time")
            .IsRequired();

        builder.Property(x => x.UpdateDateTime)
            .HasColumnName("update_date_time")
            .IsRequired();

        builder.HasIndex(x => x.ProjectId);
        builder.HasIndex(x => x.Type);
    }
}
