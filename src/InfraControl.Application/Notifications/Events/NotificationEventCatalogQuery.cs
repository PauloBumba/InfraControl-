using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Notifications.Events;

/// <summary>
/// Fase 05 (Plano Mestre §10): catálogo de eventos para o n8n — leitura
/// pura do <see cref="NotificationEventCatalog"/> (fonte única em memória,
/// sem banco). O workflow do n8n consulta este endpoint uma vez para montar
/// o switch de ramificação por <c>eventType</c>.
/// </summary>
public sealed record NotificationEventCatalogDto(
    IReadOnlyList<NotificationEventCatalogEntryDto> Events);

public sealed record NotificationEventCatalogEntryDto(
    string EventType,
    string Category,
    string Description);

public sealed record GetNotificationEventCatalogQuery
    : BaseQuery<NotificationEventCatalogDto>, IAuthorizedRequest
{
    // Catálogo em memória — cache longo é seguro (muda só com release).
    public override TimeSpan? CacheDuration => TimeSpan.FromHours(1);

    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Read;
}

public sealed class GetNotificationEventCatalogQueryHandler
    : IRequestHandler<GetNotificationEventCatalogQuery, NotificationEventCatalogDto>
{
    public Task<NotificationEventCatalogDto> Handle(
        GetNotificationEventCatalogQuery request,
        CancellationToken cancellationToken)
        => Task.FromResult(new NotificationEventCatalogDto(
            NotificationEventCatalog.All
                .Select(entry => new NotificationEventCatalogEntryDto(
                    entry.EventType, entry.Category, entry.Description))
                .ToArray()));
}
