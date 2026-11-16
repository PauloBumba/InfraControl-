using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

/// <summary>
/// ADR-070/F58: política de ruído (linha única; colunas de estabilização
/// nesta fase — lembretes/rate-limit/manutenção entram em fases
/// seguintes da F58 como colunas aditivas).
/// </summary>
public sealed class NotificationPolicySettingConfiguration
    : IEntityTypeConfiguration<NotificationPolicySetting>
{
    public void Configure(EntityTypeBuilder<NotificationPolicySetting> builder)
    {
        builder.ToTable("notification_policy_settings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.StabilizationWarningMinutes)
            .HasColumnName("stabilization_warning_minutes")
            .IsRequired();

        builder.Property(x => x.StabilizationCriticalMinutes)
            .HasColumnName("stabilization_critical_minutes")
            .IsRequired();

        builder.Property(x => x.CreateDateTime)
            .HasColumnName("create_date_time");

        builder.Property(x => x.UpdateDateTime)
            .HasColumnName("update_date_time");
    }
}
