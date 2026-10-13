using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class UserSettingsConfiguration
    : IEntityTypeConfiguration<UserSettings>
{
    public void Configure(EntityTypeBuilder<UserSettings> builder)
    {
        builder.ToTable("user_settings");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.UserId)
            .HasColumnName("user_id");

        builder.Property(x => x.Timezone)
            .HasColumnName("timezone")
            .HasMaxLength(UserSettings.TimezoneMaxLength);

        builder.Property(x => x.DarkMode)
            .HasColumnName("dark_mode");

        builder.Property(x => x.AutoRefresh)
            .HasColumnName("auto_refresh");

        builder.Property(x => x.Telemetry)
            .HasColumnName("telemetry");

        // Settings pertence ao usuário — morre com ele.
        builder.HasOne<Domain.Entities.User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Uma config por usuário.
        builder.HasIndex(x => x.UserId)
            .IsUnique()
            .HasDatabaseName("ux_user_settings_user_id");
    }
}
