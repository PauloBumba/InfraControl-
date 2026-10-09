using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class PdiResultConfiguration
    : IEntityTypeConfiguration<PdiResult>
{
    public void Configure(EntityTypeBuilder<PdiResult> builder)
    {
        builder.ToTable("pdi_results");

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

        // Resultado final é único por plano (§16).
        builder.HasIndex(x => x.PlanId)
            .IsUnique();

        builder.Property(x => x.Outcome)
            .HasColumnName("outcome")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasColumnName("notes")
            .HasMaxLength(PdiResult.MaxNotesLength);

        builder.Property(x => x.FinalizedById)
            .HasColumnName("finalized_by")
            .IsRequired();

        builder.Property(x => x.FinalizedAt)
            .HasColumnName("finalized_at")
            .IsRequired();

        builder.Property(x => x.CreateDateTime)
            .HasColumnName("create_date_time")
            .IsRequired();

        builder.Property(x => x.UpdateDateTime)
            .HasColumnName("update_date_time")
            .IsRequired();
    }
}
