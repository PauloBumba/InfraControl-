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

        // Política global (null) ou por serviço; Service Restrict — não
        // apagar um serviço com policies (pré-check 409 no handler + FK).
        builder.HasOne<Service>()
            .WithMany()
            .HasForeignKey(x => x.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
