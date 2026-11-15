using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class SlaPolicyConfiguration : IEntityTypeConfiguration<SlaPolicy>
{
    public void Configure(EntityTypeBuilder<SlaPolicy> builder)
    {
        builder.ToTable("sla_policies");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(SlaPolicy.NameMaxLength)
            .IsRequired();

        builder.Property(x => x.ResponseMinutes)
            .HasColumnName("response_minutes");

        builder.Property(x => x.ResolutionMinutes)
            .HasColumnName("resolution_minutes");

        builder.Property(x => x.ServiceId)
            .HasColumnName("service_id");

        builder.Property(x => x.IsDefault)
            .HasColumnName("is_default")
            .IsRequired();

        // No máximo uma política padrão global.
        builder.HasIndex(x => x.IsDefault)
            .IsUnique()
            .HasFilter("is_default AND service_id IS NULL");

        // Política global (null) ou por serviço; Service Restrict — não
        // apagar um serviço com policies (pré-check 409 no handler + FK).
        builder.HasOne<Service>()
            .WithMany()
            .HasForeignKey(x => x.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
