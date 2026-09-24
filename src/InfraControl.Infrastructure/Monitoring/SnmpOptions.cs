namespace InfraControl.Infrastructure.Monitoring;

/// <summary>
/// Opções SNMPv2c (F5-T004). Community default "public" — credencial por
/// device fica no F11 Credential Store.
/// </summary>
public sealed class SnmpOptions
{
    public const string SectionName = "Monitoring:Snmp";

    public string Community { get; set; } = "public";
    public int Port { get; set; } = 161;
}
