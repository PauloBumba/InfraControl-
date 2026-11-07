using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Notifications.Channels;
using InfraControl.Application.Notifications.Events;
using InfraControl.Application.Notifications.Routing.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Common;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Options;

namespace InfraControl.Application.Notifications.Routing;

/// <summary>Par de tag (chave/valor) do filtro de regra, na API.</summary>
public sealed record NotificationRoutingTagDto(string Key, string Value);

/// <summary>Regra de roteamento para a UI/API.</summary>
public sealed record NotificationRoutingRuleDto(
    Guid Id,
    string EventType,
    bool Enabled,
    int? MinSeverity,
    int? MaxSeverity,
    IReadOnlyList<string> HostIds,
    IReadOnlyList<string> HostGroupIds,
    string? TriggerId,
    IReadOnlyList<NotificationRoutingTagDto> Tags,
    IReadOnlyList<string> Channels);

/// <summary>
/// Estado da política. <c>Version = 0</c> = política nunca gravada (fanout
/// legado); <c>Enabled = false</c> = política existente desabilitada.
/// </summary>
public sealed record NotificationRoutingDto(
    bool Enabled,
    IReadOnlyList<string> Defaults,
    int Version,
    IReadOnlyList<NotificationRoutingRuleDto> Rules);

/// <summary>F39-T003: lê a política efetiva (Administer, cacheável).</summary>
public sealed record GetNotificationRoutingQuery
    : BaseQuery<NotificationRoutingDto>, IAuthorizedRequest
{
    public override IReadOnlyCollection<string> CacheTags => ["notification-routing"];

    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
}

/// <summary>Par de tag do filtro, no payload de gravação.</summary>
public sealed record SaveNotificationRoutingTagRequest(string Key, string Value);

/// <summary>Regra do payload de gravação. <c>Id = null</c> cria; id conhecido atualiza; id ausente é removido.</summary>
public sealed record SaveNotificationRoutingRuleRequest(
    Guid? Id,
    string EventType,
    bool Enabled,
    int? MinSeverity,
    int? MaxSeverity,
    IReadOnlyList<string>? HostIds,
    IReadOnlyList<string>? HostGroupIds,
    string? TriggerId,
    IReadOnlyList<SaveNotificationRoutingTagRequest>? Tags,
    IReadOnlyList<string>? Channels);

/// <summary>Salva defaults + regras de uma vez (estado completo, Administer).</summary>
public sealed record SaveNotificationRoutingCommand(
    bool Enabled,
    IReadOnlyList<string>? Defaults,
    IReadOnlyList<SaveNotificationRoutingRuleRequest>? Rules)
    : BaseCommand<NotificationRoutingDto>, IAuthorizedRequest, IAuditableRequest, ICacheInvalidatingRequest
{
    public override IReadOnlyCollection<string> CacheKeysToInvalidate => ["notification-routing"];

    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;

    public string AuditResource => "NotificationRouting";
}

public sealed class GetNotificationRoutingQueryHandler(
    INotificationRoutingRepository repository,
    IOptions<NotificationChannelsOptions> channelsOptions)
    : IRequestHandler<GetNotificationRoutingQuery, NotificationRoutingDto>
{
    public async Task<NotificationRoutingDto> Handle(
        GetNotificationRoutingQuery request, CancellationToken cancellationToken)
    {
        var state = await repository.GetStateAsync(cancellationToken);
        return Map(state, channelsOptions.Value.Default);
    }

    internal static NotificationRoutingDto Map(
        NotificationRoutingState state, IReadOnlyList<string> legacyDefaults)
    {
        var policy = state.Policy;
        var defaults = policy?.DefaultChannelNames.ToArray()
                       ?? legacyDefaults.ToArray();

        var rules = state.Rules
            .Select(r => new NotificationRoutingRuleDto(
                r.Id,
                r.EventType,
                r.Enabled,
                r.MinSeverity,
                r.MaxSeverity,
                r.HostIdList,
                r.HostGroupIdList,
                r.TriggerId,
                r.GetTagFilters()
                    .Select(t => new NotificationRoutingTagDto(t.Key, t.Value))
                    .ToArray(),
                r.ChannelNames))
            .OrderBy(r => r.EventType, StringComparer.Ordinal)
            .ThenBy(r => r.Id)
            .ToArray();

        return new NotificationRoutingDto(
            policy?.Enabled ?? false,
            defaults,
            policy?.Version ?? 0,
            rules);
    }
}

public sealed class SaveNotificationRoutingCommandHandler(
    INotificationRoutingRepository repository)
    : IRequestHandler<SaveNotificationRoutingCommand, NotificationRoutingDto>
{
    public async Task<NotificationRoutingDto> Handle(
        SaveNotificationRoutingCommand request, CancellationToken cancellationToken)
    {
        var defaults = ValidateChannels(
            request.Defaults ?? Array.Empty<string>(), "Defaults");

        var defaultsCsv = string.Join(',', defaults);
        var state = await repository.GetStateAsync(cancellationToken);

        NotificationRoutingPolicy policy;
        if (state.Policy is null)
            policy = NotificationRoutingPolicy.Create(request.Enabled, defaultsCsv);
        else
        {
            policy = state.Policy;
            policy.Update(request.Enabled, defaultsCsv);
        }

        var incoming = request.Rules ?? Array.Empty<SaveNotificationRoutingRuleRequest>();
        var existingById = state.Rules.ToDictionary(r => r.Id);
        var kept = new List<NotificationRoutingRule>();

        foreach (var payload in incoming)
        {
            var eventType = ValidateEventType(payload.EventType);
            var channels = ValidateChannels(
                payload.Channels ?? Array.Empty<string>(), "Channels");
            var hostIds = ValidateIdList(payload.HostIds, "HostIds");
            var hostGroupIds = ValidateIdList(payload.HostGroupIds, "HostGroupIds");
            var tags = ValidateTags(payload.Tags);
            ValidateSeverityRange(payload.MinSeverity, payload.MaxSeverity);

            var tagsJson = tags.Count > 0
                ? System.Text.Json.JsonSerializer.Serialize(
                    tags.Select(t => new NotificationRoutingTag(t.Key, t.Value)).ToList())
                : "[]";

            var channelsCsv = string.Join(',', channels);

            if (payload.Id is { } id && existingById.TryGetValue(id, out var existing))
            {
                existing.Update(
                    eventType, payload.Enabled, payload.MinSeverity, payload.MaxSeverity,
                    string.Join(',', hostIds), string.Join(',', hostGroupIds),
                    payload.TriggerId, tagsJson, channelsCsv);
                kept.Add(existing);
            }
            else
            {
                var rule = NotificationRoutingRule.Create(
                    eventType, payload.Enabled, payload.MinSeverity, payload.MaxSeverity,
                    string.Join(',', hostIds), string.Join(',', hostGroupIds),
                    payload.TriggerId, tagsJson, channelsCsv);
                kept.Add(rule);
            }
        }

        await repository.SaveStateAsync(policy, kept, cancellationToken);

        var saved = await repository.GetStateAsync(cancellationToken);
        return GetNotificationRoutingQueryHandler.Map(
            saved, Array.Empty<string>());
    }

    private static string ValidateEventType(string eventType)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            throw new ArgumentException(
                "O tipo de evento é obrigatório.", nameof(eventType));

        if (!NotificationEventCatalog.EventTypes.Contains(
                eventType, StringComparer.Ordinal))
            throw new ArgumentException(
                $"Tipo de evento desconhecido: '{eventType}'.",
                nameof(eventType));

        return eventType.Trim();
    }

    private static IReadOnlyList<string> ValidateChannels(
        IReadOnlyList<string> channels, string field)
    {
        var validated = new List<string>();
        foreach (var channel in channels)
        {
            if (!Enum.TryParse<NotificationChannel>(channel, ignoreCase: true, out var parsed)
                || !Enum.IsDefined(parsed))
                throw new ArgumentException(
                    $"Canal de notificação inválido: '{channel}'.", field);

            if (!validated.Contains(parsed.ToString(), StringComparer.Ordinal))
                validated.Add(parsed.ToString());
        }

        return validated;
    }

    private static IReadOnlyList<string> ValidateIdList(
        IReadOnlyList<string>? ids, string field)
    {
        if (ids is null || ids.Count == 0)
            return Array.Empty<string>();

        foreach (var id in ids)
        {
            if (string.IsNullOrWhiteSpace(id) || !id.All(char.IsAsciiDigit))
                throw new ArgumentException(
                    $"ID do Zabbix inválido: '{id}'.", field);
        }

        return ids.Select(i => i.Trim()).ToArray();
    }

    private static IReadOnlyList<NotificationRoutingTag> ValidateTags(
        IReadOnlyList<SaveNotificationRoutingTagRequest>? tags)
    {
        if (tags is null || tags.Count == 0)
            return Array.Empty<NotificationRoutingTag>();

        var validated = new List<NotificationRoutingTag>();
        foreach (var tag in tags)
        {
            if (string.IsNullOrWhiteSpace(tag.Key))
                throw new ArgumentException(
                    "A chave da tag é obrigatória.", nameof(tags));

            validated.Add(new NotificationRoutingTag(tag.Key.Trim(), tag.Value?.Trim() ?? string.Empty));
        }

        return validated;
    }

    private static void ValidateSeverityRange(int? min, int? max)
    {
        if (min is < 0 or > 5)
            throw new ArgumentException(
                "A severidade mínima deve estar entre 0 e 5.", "MinSeverity");

        if (max is < 0 or > 5)
            throw new ArgumentException(
                "A severidade máxima deve estar entre 0 e 5.", "MaxSeverity");

        if (min is not null && max is not null && min > max)
            throw new ArgumentException(
                "A severidade mínima não pode ser maior que a máxima.", "MinSeverity");
    }
}
