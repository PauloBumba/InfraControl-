using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

/// <summary>
/// F26 Fase 2.2: configuração UniFi persistida (linha única; senha cifrada
/// AES-GCM via ICredentialProtector — texto puro nunca toca o banco).
/// </summary>
public sealed class UniFiIntegrationSettingConfiguration
    : IEntityTypeConfiguration<UniFiIntegrationSetting>
{
    public void Configure(EntityTypeBuilder<UniFiIntegrationSetting> builder)
    {
        builder.ToTable("unifi_integration_settings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.BaseUrl)
            .HasColumnName("base_url")
            .HasMaxLength(UniFiIntegrationSetting.MaxLength)
            .IsRequired();

        builder.Property(x => x.Username)
            .HasColumnName("username")
            .HasMaxLength(UniFiIntegrationSetting.MaxLength)
            .IsRequired();

        builder.Property(x => x.PasswordEncrypted)
            .HasColumnName("password_encrypted")
            .HasMaxLength(UniFiIntegrationSetting.MaxLength * 4)
            .IsRequired();

        builder.Property(x => x.Site)
            .HasColumnName("site")
            .HasMaxLength(UniFiIntegrationSetting.MaxLength)
            .IsRequired();

        builder.Property(x => x.AllowInvalidTlsCertificate)
            .HasColumnName("allow_invalid_tls_certificate")
            .IsRequired();

        builder.Property(x => x.CreateDateTime)
            .HasColumnName("create_date_time");

        builder.Property(x => x.UpdateDateTime)
            .HasColumnName("update_date_time");
    }
}
