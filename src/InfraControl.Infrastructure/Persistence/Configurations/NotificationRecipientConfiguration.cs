using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class NotificationRecipientConfiguration
    : IEntityTypeConfiguration<NotificationRecipient>
{
    public void Configure(EntityTypeBuilder<NotificationRecipient> builder)
    {
        builder.ToTable("notification_recipients");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.NotificationId)
            .HasColumnName("notification_id");

        builder.Property(x => x.UserId)
            .HasColumnName("user_id");

        builder.Property(x => x.Role)
            .HasColumnName("role")
            .HasMaxLength(64);

        // Destinatário pertence à notificação.
        builder.HasOne<Notification>()
            .WithMany()
            .HasForeignKey(x => x.NotificationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Dedup: no máximo um destinatário usuário/role por notificação
        // (NULLs são distintos em Postgres/Sqlite).
        builder.HasIndex(x => new { x.NotificationId, x.UserId })
            .IsUnique()
            .HasDatabaseName("ux_notification_recipients_notification_user");

        builder.HasIndex(x => new { x.NotificationId, x.Role })
            .IsUnique()
            .HasDatabaseName("ux_notification_recipients_notification_role");
    }
}
