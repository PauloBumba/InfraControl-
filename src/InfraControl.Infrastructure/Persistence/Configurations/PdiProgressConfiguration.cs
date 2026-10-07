using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class PdiProgressConfiguration
    : IEntityTypeConfiguration<PdiProgress>
{
    public void Configure(EntityTypeBuilder<PdiProgress> builder)
    {
        builder.ToTable("pdi_progresses");

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

        // Autor sem FK — mesmo critério de ActorId na timeline (ADR-029 §4).
        builder.Property(x => x.AuthorId)
            .HasColumnName("author_id")
            .IsRequired();

        builder.Property(x => x.Percent)
            .HasColumnName("percent");

        builder.Property(x => x.Comment)
            .HasColumnName("comment")
            .HasMaxLength(PdiProgress.CommentMaxLength)
            .IsRequired();

        builder.Property(x => x.CreateDateTime)
            .HasColumnName("create_date_time")
            .IsRequired();

        builder.Property(x => x.UpdateDateTime)
            .HasColumnName("update_date_time")
            .IsRequired();
    }
}
