using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class PdiCheckInConfiguration
    : IEntityTypeConfiguration<PdiCheckIn>
{
    public void Configure(EntityTypeBuilder<PdiCheckIn> builder)
    {
        builder.ToTable("pdi_check_ins");

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

        builder.Property(x => x.CheckInDate)
            .HasColumnName("check_in_date")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .HasColumnName("created_by")
            .IsRequired();

        builder.Property(x => x.WhatDone)
            .HasColumnName("what_done")
            .HasMaxLength(PdiCheckIn.TextMaxLength)
            .IsRequired();

        builder.Property(x => x.WhatLearned)
            .HasColumnName("what_learned")
            .HasMaxLength(PdiCheckIn.TextMaxLength);

        builder.Property(x => x.Difficulty)
            .HasColumnName("difficulty")
            .HasMaxLength(PdiCheckIn.TextMaxLength);

        builder.Property(x => x.NeedSupport)
            .HasColumnName("need_support")
            .HasMaxLength(PdiCheckIn.TextMaxLength);

        builder.Property(x => x.ManagerFeedback)
            .HasColumnName("manager_feedback")
            .HasMaxLength(PdiCheckIn.TextMaxLength);

        builder.Property(x => x.NextAction)
            .HasColumnName("next_action")
            .HasMaxLength(PdiCheckIn.TextMaxLength);

        builder.Property(x => x.RespondedAt)
            .HasColumnName("responded_at");

        builder.Property(x => x.RespondedById)
            .HasColumnName("responded_by");

        builder.Property(x => x.CreateDateTime)
            .HasColumnName("create_date_time")
            .IsRequired();

        builder.Property(x => x.UpdateDateTime)
            .HasColumnName("update_date_time")
            .IsRequired();
    }
}
