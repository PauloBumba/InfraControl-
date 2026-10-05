using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

public sealed class IndividualDevelopmentPlanConfiguration
    : IEntityTypeConfiguration<IndividualDevelopmentPlan>
{
    public void Configure(EntityTypeBuilder<IndividualDevelopmentPlan> builder)
    {
        builder.ToTable("individual_development_plans");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        // 1 PDI vigente por usuário (decisão 2026-10-03) — o upsert
        // depende deste unicidade a nível de banco.
        builder.Property(x => x.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.HasIndex(x => x.UserId)
            .IsUnique();

        builder.Property(x => x.Strategy)
            .HasColumnName("strategy")
            .HasMaxLength(IndividualDevelopmentPlan.StrategyMaxLength)
            .IsRequired();

        builder.Property(x => x.TargetDate)
            .HasColumnName("target_date")
            .HasColumnType("date")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        // Gestor/liderança e período do ciclo (docs/30 §2/§20, ADR-029 §3)
        // — ambos opcionais; gestor definido só pelo Admin.
        builder.Property(x => x.ManagerId)
            .HasColumnName("manager_id");

        builder.Property(x => x.PeriodStart)
            .HasColumnName("period_start")
            .HasColumnType("date");

        builder.Property(x => x.PeriodEnd)
            .HasColumnName("period_end")
            .HasColumnType("date");

        // Buffer de domínio (drain no save) — não é navegação/relação;
        // a timeline morre em individual_development_plan_history via
        // user_id (ADR-029 §4), sem FK para o plano.
        builder.Ignore(x => x.PendingHistory);
    }
}
