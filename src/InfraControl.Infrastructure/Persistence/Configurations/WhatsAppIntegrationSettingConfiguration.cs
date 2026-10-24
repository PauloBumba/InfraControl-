using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

/// <summary>
/// ADR-062: configuração WhatsApp persistida (linha única; ApiKey cifrada
/// AES-GCM via ICredentialProtector — texto puro nunca toca o banco).
/// </summary>
public sealed class WhatsAppIntegrationSettingConfiguration
    : IEntityTypeConfiguration<WhatsAppIntegrationSetting>
{
    public void Configure(EntityTypeBuilder<WhatsAppIntegrationSetting> builder)
    {
        builder.ToTable("whatsapp_integration_settings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.BaseUrl)
            .HasColumnName("base_url")
            .HasMaxLength(WhatsAppIntegrationSetting.MaxLength)
            .IsRequired();

        builder.Property(x => x.ApiKeyEncrypted)
            .HasColumnName("api_key_encrypted")
            .HasMaxLength(WhatsAppIntegrationSetting.MaxApiKeyLength)
            .IsRequired();

        builder.Property(x => x.InstanceId)
            .HasColumnName("instance_id")
            .HasMaxLength(WhatsAppIntegrationSetting.MaxInstanceIdLength)
            .IsRequired();

        builder.Property(x => x.GroupId)
            .HasColumnName("group_id")
            .HasMaxLength(WhatsAppIntegrationSetting.MaxGroupIdLength);

        builder.Property(x => x.Phone)
            .HasColumnName("phone")
            .HasMaxLength(WhatsAppIntegrationSetting.MaxPhoneLength);

        builder.Property(x => x.NotificationsEnabled)
            .HasColumnName("notifications_enabled");

        builder.Property(x => x.ConnectionState)
            .HasColumnName("connection_state")
            .HasMaxLength(WhatsAppIntegrationSetting.MaxStateLength);

        builder.Property(x => x.LastTestedAtUtc)
            .HasColumnName("last_tested_at_utc");

        builder.Property(x => x.LastTestOk)
            .HasColumnName("last_test_ok")
            .IsRequired();

        builder.Property(x => x.LastTestError)
            .HasColumnName("last_test_error")
            .HasMaxLength(WhatsAppIntegrationSetting.MaxErrorLength);

        builder.Property(x => x.CreateDateTime)
            .HasColumnName("create_date_time");

        builder.Property(x => x.UpdateDateTime)
            .HasColumnName("update_date_time");
    }
}
