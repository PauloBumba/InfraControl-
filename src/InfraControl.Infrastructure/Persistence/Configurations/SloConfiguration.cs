using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class SloConfiguration : IEntityTypeConfiguration<Slo>
{
    public void Configure(EntityTypeBuilder<Slo> builder)
    {
        builder.ToTable("slos");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(Slo.NameMaxLength)
            .IsRequired();

        builder.Property(x => x.TargetPercent)
            .HasColumnName("target_percent")
            .HasPrecision(6, 3);

        builder.Property(x => x.WindowDays)
            .HasColumnName("window_days");

        builder.Property(x => x.ServiceId)
            .HasColumnName("service_id");

        // SLO mede objetivo de um serviço — Service Restrict (não apagar
        // um serviço com SLOs; pré-check 409 no handler + FK).
        builder.HasOne<Service>()
            .WithMany()
            .HasForeignKey(x => x.ServiceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
