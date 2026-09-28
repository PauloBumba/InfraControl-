using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class SloMeasurementConfiguration
    : IEntityTypeConfiguration<SloMeasurement>
{
    public void Configure(EntityTypeBuilder<SloMeasurement> builder)
    {
        builder.ToTable("slo_measurements");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.SloId)
            .HasColumnName("slo_id");

        builder.Property(x => x.MeasuredAt)
            .HasColumnName("measured_at");

        builder.Property(x => x.ValuePercent)
            .HasColumnName("value_percent")
            .HasPrecision(6, 3);

        // Medição pertence ao SLO — Slo Restrict (não apagar SLO medido;
        // pré-check 409 + FK).
        builder.HasOne<Slo>()
            .WithMany()
            .HasForeignKey(x => x.SloId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
