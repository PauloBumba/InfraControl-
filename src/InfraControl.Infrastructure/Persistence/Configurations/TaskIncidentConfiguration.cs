using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class TaskIncidentConfiguration : IEntityTypeConfiguration<TaskIncident>
{
    public void Configure(EntityTypeBuilder<TaskIncident> builder)
    {
        builder.ToTable("task_incidents");

        // A chave é o par (TaskId, IncidentId) — o Id da base é ignorado.
        builder.Ignore(x => x.Id);
        builder.HasKey(x => new { x.TaskId, x.IncidentId });

        builder.Property(x => x.TaskId)
            .HasColumnName("task_id");
        builder.Property(x => x.IncidentId)
            .HasColumnName("incident_id");

        // F7-T004: Task Cascade (apagar a task limpa os vínculos); Incident
        // Restrict — não há exclusão de incidente no produto, a FK é backstop.
        builder.HasOne<TaskItem>()
            .WithMany()
            .HasForeignKey(x => x.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Incident>()
            .WithMany()
            .HasForeignKey(x => x.IncidentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
