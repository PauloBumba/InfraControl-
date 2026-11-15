using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

/// <summary>
/// Agenda de relatório geral por e-mail: destinatários em JSON, frequência
/// como string, hora UTC como time. Índice único por kind (hoje só
/// "General" — singleton lógico).
/// </summary>
public sealed class ScheduledReportConfiguration
    : IEntityTypeConfiguration<ScheduledReport>
{
    public void Configure(EntityTypeBuilder<ScheduledReport> builder)
    {
        builder.ToTable("scheduled_reports");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.Kind)
            .HasColumnName("kind")
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(x => x.Kind).IsUnique();

        builder.Property(x => x.RecipientsJson)
            .HasColumnName("recipients_json")
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(x => x.Frequency)
            .HasColumnName("frequency")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.SendTimeUtc)
            .HasColumnName("send_time_utc")
            .HasColumnType("time")
            .IsRequired();

        builder.Property(x => x.DayOfWeek)
            .HasColumnName("day_of_week");

        builder.Property(x => x.DayOfMonth)
            .HasColumnName("day_of_month");

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(x => x.LastSentAtUtc)
            .HasColumnName("last_sent_at_utc");

        builder.Property(x => x.NextRunAtUtc)
            .HasColumnName("next_run_at_utc");

        builder.Property(x => x.LastError)
            .HasColumnName("last_error")
            .HasMaxLength(ScheduledReport.MaxErrorLength);

        builder.Property(x => x.CreateDateTime).HasColumnName("create_date_time");
        builder.Property(x => x.UpdateDateTime).HasColumnName("update_date_time");
    }
}
