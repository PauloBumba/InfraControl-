using InfraControl.Application.Notifications.Events;
using InfraControl.Application.Notifications.Rules.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Common;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Notifications.Rules;

/// <summary>
/// F42-T001: visão de uma regra para a UI. Canais vazio = fanout global;
/// Enabled=false = evento silenciado.
/// </summary>
public sealed record NotificationEventRuleDto(
    string EventType,
    string Category,
    string Description,
    bool Enabled,
    IReadOnlyList<string> Channels);

/// <summary>Lista completa: catálogo (13+ eventos) + regras persistidas.</summary>
public sealed record GetNotificationEventRulesQuery
    : IQuery<IReadOnlyList<NotificationEventRuleDto>>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;
}

/// <summary>Salva uma regra (upsert por EventType). Payload parcial.</summary>
public sealed record SaveNotificationEventRuleRequest(
    string EventType,
    bool Enabled,
    IReadOnlyList<string>? Channels);

public sealed record SaveNotificationEventRulesCommand(
    IReadOnlyList<SaveNotificationEventRuleRequest> Rules)
    : BaseCommand<IReadOnlyList<NotificationEventRuleDto>>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;

    public string AuditResource => "NotificationEventRules";
}

public sealed class GetNotificationEventRulesQueryHandler(
    INotificationEventRuleRepository repository)
    : IRequestHandler<GetNotificationEventRulesQuery, IReadOnlyList<NotificationEventRuleDto>>
{
    public async Task<IReadOnlyList<NotificationEventRuleDto>> Handle(
        GetNotificationEventRulesQuery request, CancellationToken cancellationToken)
    {
        var rules = await repository.ListAsync(cancellationToken);
        var byEvent = rules.ToDictionary(r => r.EventType, StringComparer.Ordinal);

        return NotificationEventCatalog.All
            .Select(entry =>
            {
                var rule = byEvent.GetValueOrDefault(entry.EventType);
                return new NotificationEventRuleDto(
                    entry.EventType,
                    entry.Category,
                    entry.Description,
                    Enabled: rule?.Enabled ?? true,
                    Channels: rule?.ChannelNames.ToArray() ?? Array.Empty<string>());
            })
            .ToArray();
    }
}

public sealed class SaveNotificationEventRulesCommandHandler(
    INotificationEventRuleRepository repository)
    : IRequestHandler<SaveNotificationEventRulesCommand, IReadOnlyList<NotificationEventRuleDto>>
{
    public async Task<IReadOnlyList<NotificationEventRuleDto>> Handle(
        SaveNotificationEventRulesCommand request, CancellationToken cancellationToken)
    {
        foreach (var payload in request.Rules)
        {
            if (string.IsNullOrWhiteSpace(payload.EventType))
                throw new ArgumentException(
                    "O tipo de evento é obrigatório.", nameof(request));

            if (!NotificationEventCatalog.EventTypes.Contains(payload.EventType, StringComparer.Ordinal))
                throw new ArgumentException(
                    $"Tipo de evento desconhecido: '{payload.EventType}'.",
                    nameof(request));

            var channels = payload.Channels is { Count: > 0 }
                ? string.Join(',', payload.Channels.Select(ValidateChannel))
                : string.Empty;

            var existing = await repository.GetByEventTypeAsync(
                payload.EventType, cancellationToken);

            if (existing is null)
            {
                if (!payload.Enabled || channels.Length > 0)
                    await repository.SaveAsync(
                        NotificationEventRule.Create(
                            payload.EventType, payload.Enabled, channels),
                        cancellationToken);
            }
            else
            {
                existing.Update(payload.EventType, payload.Enabled, channels);
                await repository.SaveAsync(existing, cancellationToken);
            }
        }

        return await new GetNotificationEventRulesQueryHandler(repository)
            .Handle(new GetNotificationEventRulesQuery(), cancellationToken);
    }

    private static string ValidateChannel(string channel)
    {
        if (!Enum.TryParse<NotificationChannel>(channel, ignoreCase: true, out var parsed)
            || !Enum.IsDefined(parsed))
            throw new ArgumentException(
                $"Canal de notificação inválido: '{channel}'.",
                nameof(channel));

        return parsed.ToString();
    }
}
