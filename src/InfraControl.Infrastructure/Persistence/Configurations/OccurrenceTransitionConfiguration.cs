using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class OccurrenceTransitionConfiguration
    : IEntityTypeConfiguration<OccurrenceTransition>
{
    public void Configure(EntityTypeBuilder<OccurrenceTransition> builder)
    {
        builder.ToTable("occurrence_transitions");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.OccurrenceKey)
            .IsRequired()
            .HasMaxLength(OccurrenceTransition.KeyMaxLength);

        builder.Property(t => t.FromState)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(t => t.ToState)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(t => t.Decision)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(40);

        builder.Property(t => t.SuppressionReason)
            .HasMaxLength(OccurrenceTransition.SuppressionReasonMaxLength);

        builder.Property(t => t.TraceId)
            .HasMaxLength(OccurrenceTransition.TraceIdMaxLength);

        builder.Property(t => t.EventType)
            .IsRequired()
            .HasMaxLength(OccurrenceTransition.EventTypeMaxLength);

        builder.Property(t => t.CorrelationId);

        builder.HasIndex(t => new { t.OccurrenceKey, t.OccurredAtUtc })
            .HasDatabaseName("ix_occurrence_transitions_key_time");
    }
}
