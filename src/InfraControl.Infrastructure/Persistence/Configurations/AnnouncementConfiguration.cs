using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        builder.ToTable("announcements");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.Message)
            .HasColumnName("message")
            .HasMaxLength(Announcement.MessageMaxLength)
            .IsRequired();

        builder.Property(x => x.Fingerprint)
            .HasColumnName("fingerprint")
            .HasMaxLength(Announcement.FingerprintMaxLength)
            .IsRequired();

        builder.Property(x => x.Severity)
            .HasColumnName("severity")
            .HasMaxLength(20)
            .IsRequired()
            .HasDefaultValue("info");

        builder.Property(x => x.Priority)
            .HasColumnName("priority")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.RepeatCount)
            .HasColumnName("repeat_count")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.MaxRepeats)
            .HasColumnName("max_repeats")
            .IsRequired()
            .HasDefaultValue(3);

        builder.Property(x => x.ScheduledAt)
            .HasColumnName("scheduled_at")
            .IsRequired();

        builder.Property(x => x.AcknowledgedAt)
            .HasColumnName("acknowledged_at");

        builder.Property(x => x.CreateDateTime)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdateDateTime)
            .HasColumnName("updated_at")
            .IsRequired();

        // Índice para deduplicação por fingerprint
        builder.HasIndex(x => x.Fingerprint)
            .HasDatabaseName("IX_announcements_fingerprint");

        // Índice para consultas de anúncios pendentes (não reconhecidos)
        builder.HasIndex(x => new { x.ScheduledAt, x.AcknowledgedAt })
            .HasDatabaseName("IX_announcements_scheduled_acknowledged");

        // Índice para prioridade
        builder.HasIndex(x => x.Priority)
            .HasDatabaseName("IX_announcements_priority");
    }
}
