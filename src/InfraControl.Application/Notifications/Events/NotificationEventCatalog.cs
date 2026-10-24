namespace InfraControl.Application.Notifications.Events;

/// <summary>
/// Fase 05 (Plano Mestre §10): fonte única dos tipos de evento que viram
/// notificação e chegam ao n8n via webhook. Espelho EXATO dos consumidores
/// do <c>NotificationEventConsumer</c> — se um consumidor for adicionado
/// ou removido, o catálogo acompanha (o teste de espelho falha se divergirem).
/// O n8n usa <c>GET /api/v1/notifications/events</c> para descobrir os
/// tipos e montar o switch do workflow sem hardcode.
/// </summary>
public static class NotificationEventCatalog
{
    /// <summary>Entradas completas (EventType + categoria + descrição pt-BR).</summary>
    public static IReadOnlyList<NotificationEventCatalogEntry> All { get; } =
    [
        new(
            "UserCreatedDomainEvent", "Identity",
            "Usuário criado no InfraControl."),
        new(
            "UserUpdatedDomainEvent", "Identity",
            "Usuário atualizado (nome/e-mail)."),
        new(
            "UserDeletedDomainEvent", "Identity",
            "Usuário excluído do InfraControl."),
        new(
            "UserDisabledDomainEvent", "Identity",
            "Usuário desativado (perde acesso)."),
        new(
            "UserRoleAssignedDomainEvent", "Identity",
            "Papel (role) atribuído a um usuário."),
        new(
            "UserRoleRemovedDomainEvent", "Identity",
            "Papel (role) removido de um usuário."),
        new(
            "InvitationAcceptedDomainEvent", "Identity",
            "Convite aceito — usuário onboardado."),
        new(
            "IncidentCreatedDomainEvent", "Operational",
            "Incidente aberto no InfraControl (alerta crítico, webhook Zabbix ou abertura manual)."),
        new(
            "AlertCreatedDomainEvent", "Operational",
            "Alerta Critical criado (nativo, prevenção ou Zabbix) — o incidente é a mensagem separada."),
        new(
            "IncidentResolvedDomainEvent", "Operational",
            "Incidente resolvido (só o InfraControl decide — regra 7 ADR-028)."),
        new(
            "IncidentUpdatedDomainEvent", "Operational",
            "Transição de status do incidente (reconhecido, em tratamento, identificado, monitoramento — resolução tem evento próprio)."),
        new(
            "AlertResolvedDomainEvent", "Operational",
            "Alerta de monitoramento resolvido."),
        new(
            "PdiCreatedDomainEvent", "PDI",
            "PDI (plano de desenvolvimento individual) criado para o dono."),
        new(
            "PdiUpdatedDomainEvent", "PDI",
            "PDI atualizado (dono ou admin)."),
        new(
            "TaskItemCreatedDomainEvent", "Tasks",
            "Tarefa criada no InfraControl (backlog/kanban)."),
        new(
            "TaskAssigneeCreatedDomainEvent", "Tasks",
            "Usuário atribuído a uma tarefa."),
    ];

    /// <summary>Só os EventType (para assertions/switch simplificado).</summary>
    public static IReadOnlyList<string> EventTypes { get; } =
        All.Select(entry => entry.EventType).ToArray();

    /// <summary>Entradas de uma categoria (Identity/Operational/PDI).</summary>
    public static IReadOnlyList<NotificationEventCatalogEntry> ByCategory(
        string category)
        => All.Where(entry => string.Equals(
                entry.Category, category, StringComparison.OrdinalIgnoreCase))
            .ToArray();
}

/// <summary>
/// Item do catálogo exposto ao n8n: tipo estável (nameof do domain event),
/// categoria para agrupar no workflow e descrição pt-BR para documentação.
/// </summary>
public sealed record NotificationEventCatalogEntry(
    string EventType,
    string Category,
    string Description);
