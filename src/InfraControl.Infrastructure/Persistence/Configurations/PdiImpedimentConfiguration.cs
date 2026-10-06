using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class PdiImpedimentConfiguration
    : IEntityTypeConfiguration<PdiImpediment>
{
    public void Configure(EntityTypeBuilder<PdiImpediment> builder)
    {
        builder.ToTable("pdi_impediments");

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

        builder.HasIndex(x => x.PhaseId);

        builder.Property(x => x.Type)
            .HasColumnName("type")
            .HasMaxLength(PdiImpediment.TypeMaxLength)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(PdiImpediment.DescriptionMaxLength)
            .IsRequired();

        builder.Property(x => x.Impact)
            .HasColumnName("impact")
            .HasMaxLength(PdiImpediment.ImpactMaxLength);

        builder.Property(x => x.OccurredAt)
            .HasColumnName("occurred_at")
            .HasColumnType("date")
            .IsRequired();

        // Responsável sem FK — mesmo critério de ResponsibleId da fase.
        builder.Property(x => x.ResponsibleId)
            .HasColumnName("responsible_id");

        builder.Property(x => x.ActionTaken)
            .HasColumnName("action_taken")
            .HasMaxLength(PdiImpediment.ActionMaxLength);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.ResolvedAt)
            .HasColumnName("resolved_at")
            .HasColumnType("date");

        builder.Property(x => x.CreateDateTime)
            .HasColumnName("create_date_time")
            .IsRequired();

        builder.Property(x => x.UpdateDateTime)
            .HasColumnName("update_date_time")
            .IsRequired();
    }
}
