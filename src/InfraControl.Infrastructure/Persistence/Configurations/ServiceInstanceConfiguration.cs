using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class ServiceInstanceConfiguration : IEntityTypeConfiguration<ServiceInstance>
{
    public void Configure(EntityTypeBuilder<ServiceInstance> builder)
    {
        builder.ToTable("service_instances");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.Kind)
            .HasColumnName("kind")
            .HasMaxLength(ServiceInstance.KindMaxLength)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(ServiceInstance.NameMaxLength)
            .IsRequired();

        builder.Property(x => x.Host)
            .HasColumnName("host")
            .HasMaxLength(ServiceInstance.HostMaxLength);

        builder.Property(x => x.Port).HasColumnName("port");

        builder.Property(x => x.HealthUrl)
            .HasColumnName("health_url")
            .HasMaxLength(ServiceInstance.HealthUrlMaxLength);

        builder.Property(x => x.Weight).HasColumnName("weight").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();

        builder.Property(x => x.Notes)
            .HasColumnName("notes")
            .HasMaxLength(ServiceInstance.NotesMaxLength);

        builder.Property(x => x.LastStatus)
            .HasColumnName("last_status")
            .HasMaxLength(20);

        builder.Property(x => x.LastCheckedAt).HasColumnName("last_checked_at");
        builder.Property(x => x.CreateDateTime).HasColumnName("create_date_time");
        builder.Property(x => x.UpdateDateTime).HasColumnName("update_date_time");

        builder.HasIndex(x => x.Kind);
    }
}
