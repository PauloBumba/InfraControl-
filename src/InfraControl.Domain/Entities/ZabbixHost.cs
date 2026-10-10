using System.Text.Json;
using InfraControl.Domain.Common;
using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// F27 Fase 3.1: host do Zabbix sincronizado para o InfraControl
/// (read-model — nunca é apagado; host removido no Zabbix vira
/// <see cref="MissingSinceUtc"/> e preserva histórico/relacionamentos,
/// mesmo critério do UniFiSite). Guarda os atributos relevantes do
/// host.get (identificador externo estável, nome técnico/visível,
/// interface principal, estado, discovery) e os nomes de grupos/
/// templates como JSON para a UI filtrar sem join no Zabbix.
/// </summary>
public sealed class ZabbixHost : Entity
{
    public const int MaxLength = 512;
    public const int MaxIpLength = 45;
    public const int MaxJsonLength = 4000;

    public string HostId { get; private set; } = string.Empty;
    public string Host { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Ip { get; private set; }
    public int? Port { get; private set; }
    public bool Enabled { get; private set; }
    public bool Discovered { get; private set; }
    public string GroupsJson { get; private set; } = "[]";
    public string TemplatesJson { get; private set; } = "[]";
    public DateTime LastSyncedAtUtc { get; private set; }
    public DateTime? MissingSinceUtc { get; private set; }

    private ZabbixHost() { }

    public static ZabbixHost Create(
        string hostId,
        string host,
        string name,
        string? ip,
        int? port,
        bool enabled,
        bool discovered,
        IReadOnlyList<string> groups,
        IReadOnlyList<string> templates,
        DateTime syncedAtUtc)
    {
        var entity = new ZabbixHost();
        entity.Update(
            hostId, host, name, ip, port, enabled, discovered,
            groups, templates, syncedAtUtc);
        return entity;
    }

    /// <summary>
    /// Atualiza os atributos a partir do host atual do Zabbix e limpa a
    /// marcação de ausência (host voltou a existir).
    /// </summary>
    public void Update(
        string hostId,
        string host,
        string name,
        string? ip,
        int? port,
        bool enabled,
        bool discovered,
        IReadOnlyList<string> groups,
        IReadOnlyList<string> templates,
        DateTime syncedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(hostId))
            throw new ArgumentException(
                "O hostid do Zabbix é obrigatório.", nameof(hostId));

        if (hostId.Length > 32)
            throw new ArgumentException(
                "O hostid do Zabbix pode ter no máximo 32 caracteres.",
                nameof(hostId));

        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException(
                "O nome técnico do host é obrigatório.", nameof(host));

        HostId = hostId.Trim();
        Host = host.Trim();
        Name = string.IsNullOrWhiteSpace(name) ? host.Trim() : name.Trim();

        var ipTrimmed = string.IsNullOrWhiteSpace(ip) ? null : ip.Trim();
        if (ipTrimmed is not null && ipTrimmed.Length > MaxIpLength)
            throw new ArgumentException(
                $"O IP pode ter no máximo {MaxIpLength} caracteres.",
                nameof(ip));
        Ip = ipTrimmed;
        Port = port;

        Enabled = enabled;
        Discovered = discovered;
        GroupsJson = SerializeList(groups);
        TemplatesJson = SerializeList(templates);
        LastSyncedAtUtc = syncedAtUtc;
        MissingSinceUtc = null;
        Touch();
    }

    /// <summary>Host sumiu do Zabbix — marcado como ausente, nunca apagado.</summary>
    public void MarkMissing()
    {
        MissingSinceUtc ??= AppTimeProvider.Current.GetUtcNow().UtcDateTime;
        Touch();
    }

    public IReadOnlyList<string> ReadGroups() => DeserializeList(GroupsJson);

    public IReadOnlyList<string> ReadTemplates() => DeserializeList(TemplatesJson);

    private static string SerializeList(IReadOnlyList<string> values)
    {
        var json = JsonSerializer.Serialize(
            values?.Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? []);
        return json.Length > MaxJsonLength
            ? throw new ArgumentException(
                $"A lista de nomes excede {MaxJsonLength} caracteres.")
            : json;
    }

    private static IReadOnlyList<string> DeserializeList(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
