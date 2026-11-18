using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class MessagingQueuePauseConfiguration
    : IEntityTypeConfiguration<MessagingQueuePause>
{
    public void Configure(EntityTypeBuilder<MessagingQueuePause> builder)
    {
        builder.ToTable("messaging_queue_pauses");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.QueueName).HasColumnName("queue_name")
            .HasMaxLength(MessagingQueuePause.QueueNameMaxLength).IsRequired();
        builder.HasIndex(x => x.QueueName).IsUnique();

        builder.Property(x => x.Vhost).HasColumnName("vhost")
            .HasMaxLength(MessagingQueuePause.VhostMaxLength).IsRequired();
        builder.Property(x => x.ConnectionNames).HasColumnName("connection_names")
            .HasMaxLength(8000).IsRequired();
        builder.Property(x => x.PausedAtUtc).HasColumnName("paused_at_utc").IsRequired();
        builder.Property(x => x.PausedByUserId).HasColumnName("paused_by_user_id")
            .HasMaxLength(64);
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500);

        builder.Property(x => x.CreateDateTime).HasColumnName("create_date_time");
        builder.Property(x => x.UpdateDateTime).HasColumnName("update_date_time");
    }
}
