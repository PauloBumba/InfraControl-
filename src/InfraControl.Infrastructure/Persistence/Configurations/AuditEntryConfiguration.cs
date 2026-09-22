using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntries");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Action).HasMaxLength(200).IsRequired();
        builder.Property(entry => entry.Resource).HasMaxLength(200).IsRequired();
        builder.Property(entry => entry.Outcome).HasMaxLength(32).IsRequired();
        builder.Property(entry => entry.ActorId).HasMaxLength(128);
        builder.Property(entry => entry.CorrelationId).HasMaxLength(128);
        builder.Property(entry => entry.Error).HasMaxLength(256);
        builder.HasIndex(entry => entry.OccurredAt);
        builder.HasIndex(entry => new { entry.Resource, entry.Action, entry.OccurredAt });
    }
}
