using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

/// <summary>
/// F27 Fase 2.2: configuração Zabbix persistida (linha única; senha cifrada
/// AES-GCM via ICredentialProtector — texto puro nunca toca o banco).
/// </summary>
public sealed class ZabbixIntegrationSettingConfiguration
    : IEntityTypeConfiguration<ZabbixIntegrationSetting>
{
    public void Configure(EntityTypeBuilder<ZabbixIntegrationSetting> builder)
    {
        builder.ToTable("zabbix_integration_settings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.ApiUrl)
            .HasColumnName("api_url")
            .HasMaxLength(ZabbixIntegrationSetting.MaxLength)
            .IsRequired();

        builder.Property(x => x.Username)
            .HasColumnName("username")
            .HasMaxLength(ZabbixIntegrationSetting.MaxLength)
            .IsRequired();

        builder.Property(x => x.PasswordEncrypted)
            .HasColumnName("password_encrypted")
            .HasMaxLength(ZabbixIntegrationSetting.MaxLength * 4)
            .IsRequired();

        builder.Property(x => x.DetectedVersion)
            .HasColumnName("detected_version")
            .HasMaxLength(ZabbixIntegrationSetting.MaxVersionLength);

        builder.Property(x => x.LastCommunicationAtUtc)
            .HasColumnName("last_communication_at_utc");

        builder.Property(x => x.LastInventorySyncAtUtc)
            .HasColumnName("last_inventory_sync_at_utc");

        builder.Property(x => x.LastTestedAtUtc)
            .HasColumnName("last_tested_at_utc");

        builder.Property(x => x.LastTestOk)
            .HasColumnName("last_test_ok")
            .IsRequired();

        builder.Property(x => x.LastTestError)
            .HasColumnName("last_test_error")
            .HasMaxLength(ZabbixIntegrationSetting.MaxErrorLength);

        builder.Property(x => x.CreateDateTime)
            .HasColumnName("create_date_time");

        builder.Property(x => x.UpdateDateTime)
            .HasColumnName("update_date_time");
    }
}
