using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Monitoring.Events;

/// <summary>
/// ADR-068: chave de correlação do problema lógico — identidade estável
/// entre flaps/updates do mesmo trigger. Primária = triggerId do contexto
/// Zabbix (já persistido em SourceContextJson pelo eventContextReader);
/// fallback = device/host/ip + hash do nome normalizado (cobre API fora do
/// ar e fontes sem triggerid).
/// </summary>
public static class MonitoringCorrelationKey
{
    /// <summary>Resolve a chave para uma ingestão de PROBLEM.</summary>
    public static string Resolve(
        MonitoringSource source,
        string? sourceContextJson,
        Guid? deviceId,
        string? host,
        string? ip,
        string name)
    {
        var triggerId = TryGetTriggerId(sourceContextJson);
        if (!string.IsNullOrWhiteSpace(triggerId))
            return $"{source}:trigger:{triggerId.Trim()}";

        var scope = deviceId?.ToString()
            ?? host?.Trim()
            ?? ip?.Trim()
            ?? "global";
        var hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(NormalizeName(name))))[..16]
            .ToLowerInvariant();

        return $"{source}:name:{scope}:{hash}";
    }

    /// <summary>
    /// Extrai o triggerId do SourceContextJson serializado
    /// (ZabbixEventContextDto — PascalCase por padrão do System.Text.Json;
    /// leitura case-insensitive por tolerância).
    /// </summary>
    public static string? TryGetTriggerId(string? sourceContextJson)
    {
        if (string.IsNullOrWhiteSpace(sourceContextJson))
            return null;

        try
        {
            using var document = JsonDocument.Parse(sourceContextJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.NameEquals("TriggerId") || property.NameEquals("triggerId"))
                    return property.Value.ValueKind == JsonValueKind.String
                        ? property.Value.GetString()
                        : null;
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static string NormalizeName(string name)
    {
        var builder = new StringBuilder(name.Length);
        var pendingSpace = false;
        foreach (var c in name.Trim().ToLowerInvariant())
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }
}
