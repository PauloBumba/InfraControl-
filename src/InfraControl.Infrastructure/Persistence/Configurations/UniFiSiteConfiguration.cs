using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

/// <summary>F26 Fase 3.1: sites do controller (unicidade em short_id).</summary>
public sealed class UniFiSiteConfiguration : IEntityTypeConfiguration<UniFiSite>
{
    public void Configure(EntityTypeBuilder<UniFiSite> builder)
    {
        builder.ToTable("unifi_sites");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.ShortId)
            .HasColumnName("short_id")
            .HasMaxLength(UniFiSite.MaxLength)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(UniFiSite.MaxLength)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(UniFiSite.MaxLength * 4)
            .IsRequired();

        builder.Property(x => x.Hidden)
            .HasColumnName("hidden")
            .IsRequired();

        builder.Property(x => x.MissingSinceUtc)
            .HasColumnName("missing_since_utc");

        builder.Property(x => x.CreateDateTime).HasColumnName("create_date_time");
        builder.Property(x => x.UpdateDateTime).HasColumnName("update_date_time");

        builder.HasIndex(x => x.ShortId).IsUnique();
    }
}
