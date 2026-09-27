using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

/// <summary>F7-T001: TaskItem — título/descrição limitados, índices de estado/tipo.</summary>
public sealed class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("tasks");

        builder.Property(x => x.Title)
            .HasMaxLength(TaskItem.TitleMaxLength)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(TaskItem.DescriptionMaxLength);

        builder.Property(x => x.Kind)
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(x => x.State)
            .HasMaxLength(20)
            .HasConversion<string>()
            .IsRequired();

        builder.HasIndex(x => x.State);
        builder.HasIndex(x => x.Kind);
    }
}
