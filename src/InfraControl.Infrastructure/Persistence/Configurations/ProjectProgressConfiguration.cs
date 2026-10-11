using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class ProjectProgressConfiguration : IEntityTypeConfiguration<ProjectProgress>
{
    public void Configure(EntityTypeBuilder<ProjectProgress> builder)
    {
        builder.ToTable("project_progresses");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(x => x.AuthorId)
            .HasColumnName("author_id")
            .IsRequired();

        builder.Property(x => x.Percent)
            .HasColumnName("percent");

        builder.Property(x => x.Comment)
            .HasColumnName("comment")
            .HasMaxLength(ProjectProgress.CommentMaxLength)
            .IsRequired();

        builder.Property(x => x.CreateDateTime)
            .HasColumnName("create_date_time")
            .IsRequired();

        builder.Property(x => x.UpdateDateTime)
            .HasColumnName("update_date_time")
            .IsRequired();

        builder.HasIndex(x => x.ProjectId);
    }
}
