using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class IncidentAlertConfiguration : IEntityTypeConfiguration<IncidentAlert>
{
    public void Configure(EntityTypeBuilder<IncidentAlert> builder)
    {
        builder.ToTable("incident_alerts");

        // A chave é o par (IncidentId, AlertId) — o Id da base é ignorado.
        builder.Ignore(x => x.Id);
        builder.HasKey(x => new { x.IncidentId, x.AlertId });

        builder.Property(x => x.IncidentId)
            .HasColumnName("incident_id");
        builder.Property(x => x.AlertId)
            .HasColumnName("alert_id");

        builder.HasOne<Incident>()
            .WithMany()
            .HasForeignKey(x => x.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Alert>()
            .WithMany()
            .HasForeignKey(x => x.AlertId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
