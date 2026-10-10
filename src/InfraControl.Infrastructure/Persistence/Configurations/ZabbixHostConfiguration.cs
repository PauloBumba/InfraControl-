using InfraControl.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InfraControl.Infrastructure.Persistence.Configurations;

/// <summary>
/// F27 Fase 3.1: host do Zabbix sincronizado (read-model; ausente =
/// MissingSinceUtc, nunca apagado — padrão UniFiSite).
/// </summary>
public sealed class ZabbixHostConfiguration : IEntityTypeConfiguration<ZabbixHost>
{
    public void Configure(EntityTypeBuilder<ZabbixHost> builder)
    {
        builder.ToTable("zabbix_hosts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.HostId)
            .HasColumnName("host_id")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Host)
            .HasColumnName("host")
            .HasMaxLength(ZabbixHost.MaxLength)
            .IsRequired();

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(ZabbixHost.MaxLength)
            .IsRequired();

        builder.Property(x => x.Ip)
            .HasColumnName("ip")
            .HasMaxLength(ZabbixHost.MaxIpLength);

        builder.Property(x => x.Port)
            .HasColumnName("port");

        builder.Property(x => x.Enabled)
            .HasColumnName("enabled")
            .IsRequired();

        builder.Property(x => x.Discovered)
            .HasColumnName("discovered")
            .IsRequired();

        builder.Property(x => x.GroupsJson)
            .HasColumnName("groups_json")
            .HasMaxLength(ZabbixHost.MaxJsonLength)
            .IsRequired();

        builder.Property(x => x.TemplatesJson)
            .HasColumnName("templates_json")
            .HasMaxLength(ZabbixHost.MaxJsonLength)
            .IsRequired();

        builder.Property(x => x.LastSyncedAtUtc)
            .HasColumnName("last_synced_at_utc")
            .IsRequired();

        builder.Property(x => x.MissingSinceUtc)
            .HasColumnName("missing_since_utc");

        builder.Property(x => x.CreateDateTime)
            .HasColumnName("create_date_time");

        builder.Property(x => x.UpdateDateTime)
            .HasColumnName("update_date_time");

        builder.HasIndex(x => x.HostId)
            .IsUnique()
            .HasDatabaseName("IX_zabbix_hosts_host_id");
    }
}
