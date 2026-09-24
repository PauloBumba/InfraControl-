namespace InfraControl.Infrastructure.Monitoring;

/// <summary>
/// Seam de SNMPv2c para providers de monitoramento (F5-T004) — permite
/// testar o walk da Printer MIB sem rede real.
/// </summary>
public interface ISnmpClient
{
    /// <summary>Walk (subtree) de um OID no device alvo.</summary>
    Task<IReadOnlyList<SnmpVariable>> WalkAsync(
        string target,
        string oid,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}

/// <summary>Variável SNMP crua: OID completo + valor já formatado.</summary>
public sealed record SnmpVariable(string Oid, string Value);
