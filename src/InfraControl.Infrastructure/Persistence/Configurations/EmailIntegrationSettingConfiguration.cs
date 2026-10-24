using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

/// <summary>
/// F41-T001: configuração SMTP persistida (linha única; senha cifrada
/// AES-GCM via ICredentialProtector — texto puro nunca toca o banco).
/// </summary>
public sealed class EmailIntegrationSettingConfiguration
    : IEntityTypeConfiguration<EmailIntegrationSetting>
{
    public void Configure(EntityTypeBuilder<EmailIntegrationSetting> builder)
    {
        builder.ToTable("email_integration_settings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.Host)
            .HasColumnName("host")
            .HasMaxLength(EmailIntegrationSetting.MaxLength)
            .IsRequired();

        builder.Property(x => x.Port)
            .HasColumnName("port")
            .IsRequired();

        builder.Property(x => x.Username)
            .HasColumnName("username")
            .HasMaxLength(EmailIntegrationSetting.MaxLength)
            .IsRequired();

        builder.Property(x => x.PasswordEncrypted)
            .HasColumnName("password_encrypted")
            .HasMaxLength(EmailIntegrationSetting.MaxLength * 4)
            .IsRequired();

        builder.Property(x => x.From)
            .HasColumnName("from_address")
            .HasMaxLength(EmailIntegrationSetting.MaxNameLength)
            .IsRequired();

        builder.Property(x => x.FromName)
            .HasColumnName("from_name")
            .HasMaxLength(EmailIntegrationSetting.MaxNameLength);

        builder.Property(x => x.UseSsl)
            .HasColumnName("use_ssl")
            .IsRequired();

        builder.Property(x => x.NotificationsEnabled)
            .HasColumnName("notifications_enabled");

        builder.Property(x => x.LastTestedAtUtc)
            .HasColumnName("last_tested_at_utc");

        builder.Property(x => x.LastTestOk)
            .HasColumnName("last_test_ok")
            .IsRequired();

        builder.Property(x => x.LastTestError)
            .HasColumnName("last_test_error")
            .HasMaxLength(EmailIntegrationSetting.MaxErrorLength);

        builder.Property(x => x.CreateDateTime)
            .HasColumnName("create_date_time");

        builder.Property(x => x.UpdateDateTime)
            .HasColumnName("update_date_time");
    }
}
