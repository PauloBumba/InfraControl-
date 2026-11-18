using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class SloCheckBindingConfiguration : IEntityTypeConfiguration<SloCheckBinding>
{
    public void Configure(EntityTypeBuilder<SloCheckBinding> builder)
    {
        builder.ToTable("slo_check_bindings");

        // A chave é o par (SloId, MonitoringCheckId) — o Id da base é ignorado.
        builder.Ignore(x => x.Id);
        builder.HasKey(x => new { x.SloId, x.MonitoringCheckId });

        builder.Property(x => x.SloId)
            .HasColumnName("slo_id");
        builder.Property(x => x.MonitoringCheckId)
            .HasColumnName("monitoring_check_id");

        // ADR-068: Restrict nos dois lados — excluir SLO ou check vinculado
        // é bloqueado (pré-check 409 no handler + backstop do banco).
        builder.HasOne<Slo>()
            .WithMany()
            .HasForeignKey(x => x.SloId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<MonitoringCheck>()
            .WithMany()
            .HasForeignKey(x => x.MonitoringCheckId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
