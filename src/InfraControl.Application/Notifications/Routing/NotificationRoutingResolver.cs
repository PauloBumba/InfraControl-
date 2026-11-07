using InfraControl.Application.Integrations.Zabbix;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Notifications.Routing;

/// <summary>
/// Origem da decisão de canais da política (docs/62 §2 — precedência
/// substitutiva aprovada no ADR-063).
/// </summary>
public enum NotificationRoutingOrigin
{
    /// <summary>Sem política persistida ou desabilitada → fanout legado (compatibilidade).</summary>
    NoPolicy = 0,

    /// <summary>Defaults globais da instalação.</summary>
    GlobalDefault = 1,

    /// <summary>Regra do tipo sem filtros (nenhuma condicional casou).</summary>
    TypeRule = 2,

    /// <summary>União deduplicada das regras condicionais que casaram.</summary>
    ConditionalUnion = 3,
}

/// <summary>
/// Entrada da resolução: tipo do evento, severidade Zabbix 0–5 (null quando
/// o evento não tiver) e contexto Zabbix persistido na ingestão (null quando
/// o lookup falhou ou a origem não é Zabbix).
/// </summary>
public sealed record NotificationRoutingInput(
    string EventType,
    int? Severity,
    ZabbixEventContextDto? Zabbix);

/// <summary>
/// Decisão efetiva da política. <see cref="Channels"/> = null apenas em
/// <see cref="NotificationRoutingOrigin.NoPolicy"/> (o fanout legado decide);
/// lista vazia = nenhum canal, sem fallback para Email.
/// </summary>
public sealed record NotificationRoutingResolution(
    IReadOnlyList<string>? Channels,
    NotificationRoutingOrigin Origin,
    int? PolicyVersion);

/// <summary>
/// F39-T003: resolução determinística pura da política (ADR-063 §proposta,
/// docs/62 §2–§3). Precedência:
/// <list type="number">
/// <item>política ausente/desabilitada → NoPolicy (legado);</item>
/// <item>regras condicionais habilitadas do tipo que casarem → união por
/// deduplicação, substituindo o default;</item>
/// <item>regra do tipo sem filtros (se existir e habilitada) → seus canais;</item>
/// <item>demais → defaults globais.</item>
/// </list>
/// Match: AND entre dimensões preenchidas, OR dentro de lista de
/// hosts/grupos; tags usam comparação ordinal sem diferenciar maiúsculas e
/// todos os pares informados devem existir no evento. Contexto ausente =
/// regra condicional não casa. Lista de canais resultante é exatamente a
/// declarada (vazia = nenhum canal; sem fallback silencioso para Email).
/// </summary>
public static class NotificationRoutingResolver
{
    public static NotificationRoutingResolution Resolve(
        NotificationRoutingPolicy? policy,
        IReadOnlyList<NotificationRoutingRule> rules,
        NotificationRoutingInput input)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(input);

        if (policy is null || !policy.Enabled)
            return new NotificationRoutingResolution(
                null, NotificationRoutingOrigin.NoPolicy, null);

        var enabledRules = rules
            .Where(r => r.Enabled
                        && string.Equals(r.EventType, input.EventType, StringComparison.Ordinal))
            .ToArray();

        var matchedConditionals = enabledRules
            .Where(r => r.IsConditional && Matches(r, input))
            .ToArray();

        if (matchedConditionals.Length > 0)
        {
            var channels = Deduplicate(
                matchedConditionals.SelectMany(r => r.ChannelNames));
            return new NotificationRoutingResolution(
                channels, NotificationRoutingOrigin.ConditionalUnion, policy.Version);
        }

        var typeRule = enabledRules.FirstOrDefault(r => !r.IsConditional);
        if (typeRule is not null)
        {
            return new NotificationRoutingResolution(
                Deduplicate(typeRule.ChannelNames),
                NotificationRoutingOrigin.TypeRule, policy.Version);
        }

        return new NotificationRoutingResolution(
            Deduplicate(policy.DefaultChannelNames),
            NotificationRoutingOrigin.GlobalDefault, policy.Version);
    }

    private static bool Matches(NotificationRoutingRule rule, NotificationRoutingInput input)
    {
        if (rule.MinSeverity is { } min)
        {
            if (input.Severity is not { } severity || severity < min)
                return false;
        }

        if (rule.MaxSeverity is { } max)
        {
            if (input.Severity is not { } severity || severity > max)
                return false;
        }

        var context = input.Zabbix;

        if (rule.HostIdList.Count > 0)
        {
            if (context is null
                || !rule.HostIdList.Any(id => context.HostIds.Contains(id, StringComparer.Ordinal)))
                return false;
        }

        if (rule.HostGroupIdList.Count > 0)
        {
            if (context is null
                || !rule.HostGroupIdList.Any(id => context.HostGroupIds.Contains(id, StringComparer.Ordinal)))
                return false;
        }

        if (rule.TriggerId is not null)
        {
            if (context is null
                || !string.Equals(context.TriggerId, rule.TriggerId, StringComparison.Ordinal))
                return false;
        }

        foreach (var tagFilter in rule.GetTagFilters())
        {
            if (context is null
                || !context.Tags.Any(t =>
                    string.Equals(t.Tag, tagFilter.Key, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(t.Value, tagFilter.Value, StringComparison.OrdinalIgnoreCase)))
                return false;
        }

        return true;
    }

    private static IReadOnlyList<string> Deduplicate(IEnumerable<string> channels)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<string>();
        foreach (var channel in channels)
        {
            if (seen.Add(channel))
                ordered.Add(channel);
        }

        return ordered;
    }
}
