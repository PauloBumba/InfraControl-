using InfraControl.Application.Network.Services;

namespace InfraControl.Infrastructure.NetworkServices.Ports;

/// <summary>
/// Driver de firewall por fabricante (F18-T005 · ADR-042 §3): cada
/// vendor responde pelo SEU comando de leitura e pelo parse da saída —
/// a expressão de cada regra fica crua (texto do device), sem semântica
/// inventada.
/// </summary>
public interface IFirewallVendorReader
{
    /// <summary>Chave do driver (ex.: "cisco").</summary>
    string Vendor { get; }

    /// <summary>Comando de leitura executado via SSH (ex.: "show access-lists").</summary>
    string Command { get; }

    IReadOnlyList<FirewallRuleDto> Parse(string commandOutput);
}

/// <summary>
/// Resolve o driver pelo vendor configurado — vendor desconhecido lança
/// <see cref="InvalidOperationException"/> pt-BR listando os suportados.
/// </summary>
public interface IFirewallVendorReaderSelector
{
    IFirewallVendorReader Resolve(string vendor);
}
