using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class SloBudgetConfiguration
    : IEntityTypeConfiguration<SloBudget>
{
    public void Configure(EntityTypeBuilder<SloBudget> builder)
    {
        builder.ToTable("slo_budgets");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.SloId)
            .HasColumnName("slo_id");

        builder.Property(x => x.PeriodStart)
            .HasColumnName("period_start");

        builder.Property(x => x.PeriodEnd)
            .HasColumnName("period_end");

        builder.Property(x => x.ConsumedPercent)
            .HasColumnName("consumed_percent")
            .HasPrecision(6, 3);

        // Orçamento pertence ao SLO — Slo Restrict (não apagar SLO com
        // orçamento; pré-check 409 + FK).
        builder.HasOne<Slo>()
            .WithMany()
            .HasForeignKey(x => x.SloId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
