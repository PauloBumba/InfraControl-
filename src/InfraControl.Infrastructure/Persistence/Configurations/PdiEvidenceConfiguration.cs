using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class PdiEvidenceConfiguration
    : IEntityTypeConfiguration<PdiEvidence>
{
    public void Configure(EntityTypeBuilder<PdiEvidence> builder)
    {
        builder.ToTable("pdi_evidences");

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

        builder.Property(x => x.CreatedBy)
            .HasColumnName("created_by")
            .IsRequired();

        builder.Property(x => x.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(PdiEvidence.MaxDescriptionLength);

        builder.Property(x => x.OriginalFileName)
            .HasColumnName("original_file_name")
            .HasMaxLength(PdiEvidence.MaxFileNameLength)
            .IsRequired();

        builder.Property(x => x.Extension)
            .HasColumnName("extension")
            .HasMaxLength(PdiEvidence.MaxExtensionLength)
            .IsRequired();

        builder.Property(x => x.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(PdiEvidence.MaxContentTypeLength)
            .IsRequired();

        builder.Property(x => x.FileSize)
            .HasColumnName("file_size")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.ReviewedBy)
            .HasColumnName("reviewed_by");

        builder.Property(x => x.ReviewedAt)
            .HasColumnName("reviewed_at");

        builder.Property(x => x.RejectionReason)
            .HasColumnName("rejection_reason")
            .HasMaxLength(PdiEvidence.MaxDescriptionLength);

        builder.Property(x => x.CreateDateTime)
            .HasColumnName("create_date_time")
            .IsRequired();

        builder.Property(x => x.UpdateDateTime)
            .HasColumnName("update_date_time")
            .IsRequired();
    }
}
