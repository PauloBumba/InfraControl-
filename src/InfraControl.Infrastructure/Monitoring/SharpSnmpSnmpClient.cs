using System.Net;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using Microsoft.Extensions.Options;

namespace InfraControl.Infrastructure.Monitoring;

/// <summary>
/// Walk SNMPv2c real via Lextm.SharpSnmpLib (F5-T004). Falhas de rede viram
/// exceção — o provider as converte em erro pt-BR.
/// </summary>
public sealed class SharpSnmpSnmpClient(IOptions<SnmpOptions> options) : ISnmpClient
{
    public async Task<IReadOnlyList<SnmpVariable>> WalkAsync(
        string target,
        string oid,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (!IPAddress.TryParse(target, out var address))
            throw new ArgumentException($"Destino inválido: '{target}' não é um IP válido.");

        var config = options.Value;
        var endpoint = new IPEndPoint(address, config.Port);
        var community = new OctetString(config.Community);
        var table = new ObjectIdentifier(oid);
        var variables = new List<Variable>();

        using var timeoutSource = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        await Messenger.WalkAsync(
            VersionCode.V2,
            endpoint,
            community,
            table,
            variables,
            WalkMode.WithinSubtree,
            timeoutSource.Token);

        return variables
            .Select(variable => new SnmpVariable(variable.Id.ToString(), variable.Data.ToString() ?? string.Empty))
            .ToArray();
    }
}
