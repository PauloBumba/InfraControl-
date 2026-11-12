using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class IncidentConfiguration : IEntityTypeConfiguration<Incident>
{
    public void Configure(EntityTypeBuilder<Incident> builder)
    {
        builder.ToTable("incidents");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.Title)
            .HasColumnName("title")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(2000);

        builder.Property(x => x.Severity)
            .HasColumnName("severity")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.DeviceId)
            .HasColumnName("device_id");

        builder.Property(x => x.ResolvedAt)
            .HasColumnName("resolved_at");

        builder.Property(x => x.RootCause)
            .HasMaxLength(Incident.RootCauseMaxLength);

        // F52-T001: baixa justificada (ServiceNow) — enum-string ADR-061.
        builder.Property(x => x.ResolutionCode)
            .HasColumnName("resolution_code")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.ResolutionNote)
            .HasColumnName("resolution_note")
            .HasMaxLength(Incident.ResolutionNoteMaxLength);

        // Incidente de device excluído acompanha (mesma política dos alerts).
        builder.HasOne<Device>()
            .WithMany()
            .HasForeignKey(x => x.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("IX_incidents_status");
        builder.HasIndex(x => x.DeviceId)
            .HasDatabaseName("IX_incidents_device_id");
    }
}
