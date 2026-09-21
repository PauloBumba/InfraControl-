using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Common.Persistence.Configurations;

public class ProcessedMessageConfiguration : IEntityTypeConfiguration<ProcessedMessage>
{
    public void Configure(EntityTypeBuilder<ProcessedMessage> builder)
    {
        builder.ToTable("processed_messages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.EventId)
            .HasColumnName("event_id")
            .IsRequired();

        builder.Property(x => x.Consumer)
            .HasColumnName("consumer")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(x => x.ProcessedAt)
            .HasColumnName("processed_at")
            .IsRequired();

        builder.HasIndex(x => new { x.EventId, x.Consumer })
            .IsUnique()
            .HasDatabaseName("ix_processed_messages_event_consumer");
    }
}
