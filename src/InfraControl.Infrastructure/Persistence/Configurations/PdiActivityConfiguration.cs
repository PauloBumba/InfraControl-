using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class PdiActivityConfiguration
    : IEntityTypeConfiguration<PdiActivity>
{
    public void Configure(EntityTypeBuilder<PdiActivity> builder)
    {
        builder.ToTable("pdi_activities");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.HasOne<PdiPhase>()
            .WithMany()
            .HasForeignKey(x => x.PhaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.PhaseId)
            .HasColumnName("phase_id")
            .IsRequired();

        builder.HasIndex(x => new { x.PhaseId, x.Order });

        builder.Property(x => x.Title)
            .HasColumnName("title")
            .HasMaxLength(PdiActivity.TitleMaxLength)
            .IsRequired();

        builder.Property(x => x.DueDate)
            .HasColumnName("due_date")
            .HasColumnType("date");

        builder.Property(x => x.Completed)
            .HasColumnName("completed")
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
