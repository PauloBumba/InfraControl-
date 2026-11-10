using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

/// <summary>
/// F39-T003: política de roteamento — linha única (singleton) com defaults
/// globais, habilitação e versão monotônica.
/// </summary>
public sealed class NotificationRoutingPolicyConfiguration
    : IEntityTypeConfiguration<NotificationRoutingPolicy>
{
    public void Configure(EntityTypeBuilder<NotificationRoutingPolicy> builder)
    {
        builder.ToTable("notification_routing_policies");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.Enabled)
            .HasColumnName("enabled")
            .IsRequired();

        builder.Property(x => x.Defaults)
            .HasColumnName("defaults")
            .HasMaxLength(NotificationRoutingPolicy.MaxDefaultsLength)
            .IsRequired();

        builder.Property(x => x.Version)
            .HasColumnName("version")
            .IsRequired();

        builder.Property(x => x.CreateDateTime)
            .HasColumnName("create_date_time");

        builder.Property(x => x.UpdateDateTime)
            .HasColumnName("update_date_time");
    }
}
