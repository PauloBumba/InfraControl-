namespace InfraControl.Infrastructure.Monitoring;

/// <summary>
/// Versão SNMP usada no walk (F30-T008 reparo impressoras HSDS).
/// </summary>
public enum SnmpVersion
{
    /// <summary>SNMPv1 — denominador comum: agentes legados (impressoras
    /// imageRUNNER etc.) só respondem v1; quem fala v2c responde v1.</summary>
    V1,

    /// <summary>SNMPv2c — GetBulk, mais eficiente em tabelas grandes.</summary>
    V2c
}

/// <summary>
/// Seam de SNMP para providers de monitoramento (F5-T004) — permite
/// testar o walk da Printer MIB sem rede real.
/// </summary>
public interface ISnmpClient
{
    /// <summary>Walk (subtree) de um OID no device alvo (SNMPv2c).</summary>
    Task<IReadOnlyList<SnmpVariable>> WalkAsync(
        string target,
        string oid,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Walk com versão SNMP explícita. O default delega ao walk v2c —
    /// implementações antigas continuam válidas; o cliente real
    /// (SharpSnmpSnmpClient) honra a versão pedida.
    /// </summary>
    Task<IReadOnlyList<SnmpVariable>> WalkAsync(
        string target,
        string oid,
        TimeSpan timeout,
        SnmpVersion version,
        CancellationToken cancellationToken = default)
        => WalkAsync(target, oid, timeout, cancellationToken);
}

/// <summary>Variável SNMP crua: OID completo + valor já formatado.</summary>
public sealed record SnmpVariable(string Oid, string Value);
