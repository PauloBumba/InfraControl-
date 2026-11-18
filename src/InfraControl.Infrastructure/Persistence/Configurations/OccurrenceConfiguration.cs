using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class OccurrenceConfiguration : IEntityTypeConfiguration<Occurrence>
{
    public void Configure(EntityTypeBuilder<Occurrence> builder)
    {
        builder.ToTable("occurrences");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.OccurrenceKey)
            .IsRequired()
            .HasMaxLength(Occurrence.KeyMaxLength);

        builder.Property(o => o.State)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(o => o.Version).IsConcurrencyToken();

        builder.HasIndex(o => o.OccurrenceKey)
            .IsUnique()
            .HasDatabaseName("ux_occurrences_key");

        builder.HasIndex(o => o.UpdatedAtUtc)
            .HasDatabaseName("ix_occurrences_updated_at");
    }
}
