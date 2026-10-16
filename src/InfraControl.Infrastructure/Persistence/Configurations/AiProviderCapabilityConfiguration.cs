using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

/// <summary>
/// ADR-057 (FAI-T011a): roteamento capacidade → provider (uma linha por
/// capacidade — índice único em <c>capability</c>).
/// </summary>
public sealed class AiProviderCapabilityConfiguration
    : IEntityTypeConfiguration<AiProviderCapability>
{
    public void Configure(EntityTypeBuilder<AiProviderCapability> builder)
    {
        builder.ToTable("ai_provider_capabilities");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.Capability)
            .HasColumnName("capability")
            .HasMaxLength(AiProviderCapability.MaxCapabilityLength)
            .IsRequired();

        builder.HasIndex(x => x.Capability)
            .IsUnique();

        builder.Property(x => x.ProviderId)
            .HasColumnName("provider_id")
            .IsRequired();

        builder.HasOne<AiProvider>()
            .WithMany()
            .HasForeignKey(x => x.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.DefaultModel)
            .HasColumnName("default_model")
            .HasMaxLength(AiProviderCapability.MaxModelLength)
            .IsRequired();

        builder.Property(x => x.FallbackProviderId)
            .HasColumnName("fallback_provider_id");

        builder.HasOne<AiProvider>()
            .WithMany()
            .HasForeignKey(x => x.FallbackProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.FallbackModel)
            .HasColumnName("fallback_model")
            .HasMaxLength(AiProviderCapability.MaxModelLength);

        builder.Property(x => x.CreateDateTime)
            .HasColumnName("create_date_time");

        builder.Property(x => x.UpdateDateTime)
            .HasColumnName("update_date_time");
    }
}
