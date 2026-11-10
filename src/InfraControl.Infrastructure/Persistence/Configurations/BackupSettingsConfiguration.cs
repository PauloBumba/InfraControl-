using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class BackupSettingsConfiguration : IEntityTypeConfiguration<BackupSettings>
{
    public void Configure(EntityTypeBuilder<BackupSettings> builder)
    {
        builder.ToTable("backup_settings");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.Enabled).HasColumnName("enabled").IsRequired();
        builder.Property(x => x.IntervalHours).HasColumnName("interval_hours").IsRequired();
        builder.Property(x => x.RetentionDays).HasColumnName("retention_days").IsRequired();
        builder.Property(x => x.Path).HasColumnName("path")
            .HasMaxLength(BackupSettings.PathMaxLength).IsRequired();
        builder.Property(x => x.FileNamePrefix).HasColumnName("file_name_prefix")
            .HasMaxLength(BackupSettings.PrefixMaxLength).IsRequired();
        builder.Property(x => x.TimeoutSeconds).HasColumnName("timeout_seconds").IsRequired();

        builder.Property(x => x.CreateDateTime).HasColumnName("create_date_time");
        builder.Property(x => x.UpdateDateTime).HasColumnName("update_date_time");
    }
}
