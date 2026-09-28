using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class TaskSlaPauseConfiguration : IEntityTypeConfiguration<TaskSlaPause>
{
    public void Configure(EntityTypeBuilder<TaskSlaPause> builder)
    {
        builder.ToTable("task_sla_pauses");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.TaskId)
            .HasColumnName("task_id");

        builder.Property(x => x.StartAt)
            .HasColumnName("start_at");

        builder.Property(x => x.EndAt)
            .HasColumnName("end_at");

        // FK para task_slas (PK = task_id): apagar o SLA/task limpa as
        // pausas — Cascade.
        builder.HasOne<TaskSla>()
            .WithMany()
            .HasForeignKey(x => x.TaskId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
