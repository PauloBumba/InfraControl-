using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class PdiHistoryEntryConfiguration
    : IEntityTypeConfiguration<PdiHistoryEntry>
{
    public void Configure(EntityTypeBuilder<PdiHistoryEntry> builder)
    {
        builder.ToTable("individual_development_plan_history");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        // Timeline do plano (1 por usuário) — agrupado por dono e
        // ordenado por momento do evento (ADR-029 §4).
        builder.Property(x => x.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.HasIndex(x => new { x.UserId, x.CreateDateTime });

        builder.Property(x => x.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.ActorId)
            .HasColumnName("actor_id")
            .IsRequired();

        builder.Property(x => x.FromStatus)
            .HasColumnName("from_status")
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(x => x.ToStatus)
            .HasColumnName("to_status")
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(x => x.Reason)
            .HasColumnName("reason")
            .HasMaxLength(PdiHistoryEntry.MaxReasonLength);

        builder.Property(x => x.Detail)
            .HasColumnName("detail")
            .HasMaxLength(PdiHistoryEntry.MaxDetailLength);

        builder.Property(x => x.CreateDateTime)
            .HasColumnName("create_date_time")
            .IsRequired();

        builder.Property(x => x.UpdateDateTime)
            .HasColumnName("update_date_time")
            .IsRequired();
    }
}
