using InfraControl.Infrastructure.Monitoring;

namespace InfraControl.Infrastructure.Discovery;

/// <summary>
/// Walk tolerante do ifName local (IF-MIB ifXTable
/// 1.3.6.1.2.1.31.1.1.1.1) — mapeia ifIndex → nome da interface (F3-T005b:
/// LLDP/CDP trazem só o ifIndex local; o casamento com NetworkInterface é
/// por NOME). Falha/timeout → dicionário vazio (achado segue sem o
/// atributo local_if_name; a ingestão de Connection apenas pula).
/// </summary>
internal static class IfNameLookup
{
    public const string TableOid = "1.3.6.1.2.1.31.1.1.1.1";
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(2);

    public static async Task<IReadOnlyDictionary<string, string>> GetAsync(
        ISnmpClient snmpClient,
        string target,
        CancellationToken cancellationToken)
    {
        try
        {
            var variables = await snmpClient.WalkAsync(
                target, TableOid, DefaultTimeout, cancellationToken);

            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var variable in variables)
            {
                if (string.IsNullOrWhiteSpace(variable.Value))
                    continue;

                var parts = variable.Oid.TrimStart('.').Split('.');
                var prefixLength = TableOid.Split('.').Length;
                // ...ifName.<ifIndex>
                if (parts.Length != prefixLength + 1)
                    continue;
                if (!string.Join('.', parts[..prefixLength]).Equals(
                        TableOid, StringComparison.Ordinal))
                    continue;

                map[parts[prefixLength]] = variable.Value;
            }

            return map;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }
}
