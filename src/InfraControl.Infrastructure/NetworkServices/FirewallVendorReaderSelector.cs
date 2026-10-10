using InfraControl.Infrastructure.NetworkServices.Ports;

namespace InfraControl.Infrastructure.NetworkServices;

/// <summary>
/// Resolve o driver de firewall pelo vendor configurado (F18-T005 ·
/// ADR-042 §3); vendor sem driver → erro pt-BR honesto listando os
/// suportados (a API responde 503 + motivo).
/// </summary>
public sealed class FirewallVendorReaderSelector(
    IEnumerable<IFirewallVendorReader> readers) : IFirewallVendorReaderSelector
{
    public IFirewallVendorReader Resolve(string vendor)
    {
        var match = readers.FirstOrDefault(reader =>
            string.Equals(reader.Vendor, vendor, StringComparison.OrdinalIgnoreCase));

        return match ?? throw new InvalidOperationException(
            $"Vendor '{vendor}' sem leitor de firewall — suportados: " +
            $"{string.Join(", ", readers.Select(reader => reader.Vendor))}.");
    }
}
