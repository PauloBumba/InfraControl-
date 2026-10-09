using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class PdiAssessmentConfiguration
    : IEntityTypeConfiguration<PdiAssessment>
{
    public void Configure(EntityTypeBuilder<PdiAssessment> builder)
    {
        builder.ToTable("pdi_assessments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.HasOne<IndividualDevelopmentPlan>()
            .WithMany()
            .HasForeignKey(x => x.PlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.PlanId)
            .HasColumnName("plan_id")
            .IsRequired();

        // 1 linha por lado (Self/Manager) — upsert pela própria linha.
        builder.HasIndex(x => new { x.PlanId, x.Side })
            .IsUnique();

        builder.Property(x => x.Side)
            .HasColumnName("side")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.AuthorId)
            .HasColumnName("author_id")
            .IsRequired();

        builder.Property(x => x.Content)
            .HasColumnName("content")
            .HasMaxLength(PdiAssessment.MaxContentLength)
            .IsRequired();

        builder.Property(x => x.CreateDateTime)
            .HasColumnName("create_date_time")
            .IsRequired();

        builder.Property(x => x.UpdateDateTime)
            .HasColumnName("update_date_time")
            .IsRequired();
    }
}
