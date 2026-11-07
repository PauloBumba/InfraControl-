using System.Text.Json;
using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>Par de tag (chave/valor) usado como filtro em regra de roteamento.</summary>
public sealed record NotificationRoutingTag(string Key, string Value);

/// <summary>
/// F39-T003 (ADR-063): regra de roteamento por tipo de evento, com filtros
/// opcionais (severidade Zabbix 0–5, host IDs, grupo IDs, trigger ID e tags
/// do evento). Várias regras condicionais por EventType são permitidas;
/// o match é AND entre dimensões preenchidas e OR dentro de uma lista
/// (docs/62 §3). Sem filtros = regra do tipo (substitui o default quando
/// nenhuma condicional casa). <see cref="Channels"/> vazio em regra
/// explícita = nenhum canal (desliga os canais externos, sem fallback para
/// Email).
/// </summary>
public sealed class NotificationRoutingRule : Entity
{
    public const int MaxEventTypeLength = 255;
    public const int MaxChannelsLength = 500;
    public const int MaxIdListLength = 2000;
    public const int MaxTriggerIdLength = 64;
    public const int MaxTagsLength = 4000;

    public string EventType { get; private set; } = string.Empty;
    public bool Enabled { get; private set; } = true;

    /// <summary>Filtro de severidade Zabbix mínima (inclusive), 0–5. Null = sem filtro.</summary>
    public int? MinSeverity { get; private set; }

    /// <summary>Filtro de severidade Zabbix máxima (inclusive), 0–5. Null = sem filtro.</summary>
    public int? MaxSeverity { get; private set; }

    /// <summary>IDs de host Zabbix (CSV, OR entre eles). Vazio = sem filtro.</summary>
    public string HostIds { get; private set; } = string.Empty;

    /// <summary>IDs de grupo de host Zabbix (CSV, OR entre eles). Vazio = sem filtro.</summary>
    public string HostGroupIds { get; private set; } = string.Empty;

    /// <summary>Trigger ID do Zabbix (igualdade ordinal). Null/empty = sem filtro.</summary>
    public string? TriggerId { get; private set; }

    /// <summary>Filtros de tag como JSON <c>[{"Key":"..","Value":".."}]</c>. Todos os pares devem existir no evento (AND).</summary>
    public string Tags { get; private set; } = "[]";

    /// <summary>Canais da regra (CSV de nomes do enum). Vazio = nenhum canal.</summary>
    public string Channels { get; private set; } = string.Empty;

    private NotificationRoutingRule() { }

    public static NotificationRoutingRule Create(
        string eventType,
        bool enabled,
        int? minSeverity,
        int? maxSeverity,
        string hostIds,
        string hostGroupIds,
        string? triggerId,
        string tags,
        string channels)
    {
        var rule = new NotificationRoutingRule();
        rule.Update(
            eventType, enabled, minSeverity, maxSeverity, hostIds, hostGroupIds,
            triggerId, tags, channels);
        return rule;
    }

    public void Update(
        string eventType,
        bool enabled,
        int? minSeverity,
        int? maxSeverity,
        string hostIds,
        string hostGroupIds,
        string? triggerId,
        string tags,
        string channels)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException(
                "O tipo de evento é obrigatório.", nameof(eventType));

        if (eventType.Length > MaxEventTypeLength)
            throw new ArgumentException(
                $"O tipo de evento pode ter no máximo {MaxEventTypeLength} caracteres.",
                nameof(eventType));

        if (minSeverity is < 0 or > 5)
            throw new ArgumentException(
                "A severidade mínima deve estar entre 0 e 5.", nameof(minSeverity));

        if (maxSeverity is < 0 or > 5)
            throw new ArgumentException(
                "A severidade máxima deve estar entre 0 e 5.", nameof(maxSeverity));

        if (minSeverity is not null && maxSeverity is not null && minSeverity > maxSeverity)
            throw new ArgumentException(
                "A severidade mínima não pode ser maior que a máxima.",
                nameof(minSeverity));

        hostIds ??= string.Empty;
        hostGroupIds ??= string.Empty;
        if (hostIds.Length > MaxIdListLength || hostGroupIds.Length > MaxIdListLength)
            throw new ArgumentException(
                $"A lista de IDs pode ter no máximo {MaxIdListLength} caracteres.",
                nameof(hostIds));

        if (triggerId is { Length: > MaxTriggerIdLength })
            throw new ArgumentException(
                $"O trigger ID pode ter no máximo {MaxTriggerIdLength} caracteres.",
                nameof(triggerId));

        tags ??= "[]";
        if (tags.Length > MaxTagsLength)
            throw new ArgumentException(
                $"A lista de tags pode ter no máximo {MaxTagsLength} caracteres.",
                nameof(tags));

        if (channels is { Length: > MaxChannelsLength })
            throw new ArgumentException(
                $"A lista de canais pode ter no máximo {MaxChannelsLength} caracteres.",
                nameof(channels));

        EventType = eventType.Trim();
        Enabled = enabled;
        MinSeverity = minSeverity;
        MaxSeverity = maxSeverity;
        HostIds = hostIds.Trim();
        HostGroupIds = hostGroupIds.Trim();
        TriggerId = string.IsNullOrWhiteSpace(triggerId) ? null : triggerId.Trim();
        Tags = tags;
        Channels = channels?.Trim() ?? string.Empty;
        Touch();
    }

    /// <summary>Canais parseados (nomes do enum), na ordem configurada.</summary>
    public IReadOnlyList<string> ChannelNames => SplitCsv(Channels);

    /// <summary>IDs de host parseados (OR entre eles).</summary>
    public IReadOnlyList<string> HostIdList => SplitCsv(HostIds);

    /// <summary>IDs de grupo parseados (OR entre eles).</summary>
    public IReadOnlyList<string> HostGroupIdList => SplitCsv(HostGroupIds);

    /// <summary>Filtros de tag parseados do JSON (AND entre os pares). Método (não propriedade) para o EF não interpretar como navegação.</summary>
    public IReadOnlyList<NotificationRoutingTag> GetTagFilters()
    {
        if (string.IsNullOrWhiteSpace(Tags))
            return Array.Empty<NotificationRoutingTag>();

        try
        {
            var parsed = JsonSerializer.Deserialize<List<NotificationRoutingTag>>(Tags);
            return parsed?.Where(t => !string.IsNullOrWhiteSpace(t.Key)).ToArray()
                   ?? Array.Empty<NotificationRoutingTag>();
        }
        catch (JsonException)
        {
            return Array.Empty<NotificationRoutingTag>();
        }
    }

    /// <summary>True quando qualquer filtro está preenchido (regra condicional).</summary>
    public bool IsConditional =>
        MinSeverity is not null
        || MaxSeverity is not null
        || HostIdList.Count > 0
        || HostGroupIdList.Count > 0
        || TriggerId is not null
        || GetTagFilters().Count > 0;

    private static IReadOnlyList<string> SplitCsv(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? Array.Empty<string>()
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
