using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Notifications.Feed.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Notifications.Feed.Queries;

// Sem cache (IQuery direto): o feed muda a cada notificação consumida.

/// <summary>
/// F18-T001 — `GET` paginado do feed do usuário autenticado (join
/// `NotificationRecipient` → `Notification`, mais recentes primeiro).
/// O usuário e as roles vêm do `ICurrentUserContext` (não da query).
/// </summary>
public sealed record GetNotificationFeedQuery(int Page = 1, int PageSize = 20)
    : IQuery<PagedResult<NotificationFeedItemDto>>, IAuthorizedRequest
{
    // ADR-016: leitura = qualquer role canônica (Viewer, Operator, Admin).
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetNotificationFeedQueryHandler(
    INotificationFeedRepository repository,
    ICurrentUserContext currentUser)
    : IRequestHandler<GetNotificationFeedQuery, PagedResult<NotificationFeedItemDto>>
{
    public async Task<PagedResult<NotificationFeedItemDto>> Handle(
        GetNotificationFeedQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException(
                "Usuário autenticado é obrigatório para o feed de notificações.");

        var result = await repository.SearchForUserAsync(
            userId, currentUser.Roles, page, pageSize, cancellationToken);

        return new PagedResult<NotificationFeedItemDto>(
            result.Items
                .Select(notification => new NotificationFeedItemDto(
                    notification.Id,
                    notification.EventType,
                    notification.Title,
                    notification.Message,
                    notification.CreateDateTime))
                .ToArray(),
            result.TotalCount,
            result.Page,
            result.PageSize);
    }
}
