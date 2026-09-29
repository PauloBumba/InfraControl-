using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class NotificationPreferenceConfiguration
    : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("notification_preferences");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.UserId)
            .HasColumnName("user_id");

        builder.Property(x => x.Channel)
            .HasColumnName("channel")
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(x => x.Enabled)
            .HasColumnName("enabled");

        // Preferência pertence ao usuário — morre com ele (F9-T003).
        builder.HasOne<Domain.Entities.User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Uma preferência por (usuário, canal).
        builder.HasIndex(x => new { x.UserId, x.Channel })
            .IsUnique()
            .HasDatabaseName("ux_notification_preferences_user_channel");
    }
}
