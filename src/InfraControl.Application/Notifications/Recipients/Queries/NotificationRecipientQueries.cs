using InfraControl.Application.Notifications.Recipients.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Notifications.Recipients.Queries;

// Sem cache (IQuery direto): destinatários são criados junto com a
// notificação no consumer.

public sealed record GetNotificationRecipientsQuery(
    Guid NotificationId,
    int Page = 1,
    int PageSize = 20)
    : IQuery<PagedResult<NotificationRecipientDto>>, IAuthorizedRequest
{
    // ADR-016: leitura = qualquer role canônica (Viewer, Operator, Admin).
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetNotificationRecipientsQueryHandler(
    INotificationRecipientRepository repository)
    : IRequestHandler<GetNotificationRecipientsQuery, PagedResult<NotificationRecipientDto>>
{
    public async Task<PagedResult<NotificationRecipientDto>> Handle(
        GetNotificationRecipientsQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var result = await repository.SearchByNotificationIdAsync(
            request.NotificationId, page, pageSize, cancellationToken);

        return new PagedResult<NotificationRecipientDto>(
            result.Items
                .Select(r => new NotificationRecipientDto(
                    r.Id, r.NotificationId, r.UserId, r.Role))
                .ToArray(),
            result.TotalCount,
            result.Page,
            result.PageSize);
    }
}

/// <summary>
/// Usuários finais únicos da notificação — roles resolvidas e dedup
/// (docs/07: múltiplas roles → única notificação lógica). Consumido pela
/// entrega nos canais (F9-T004+).
/// </summary>
public sealed record ResolveNotificationUsersQuery(Guid NotificationId)
    : IQuery<IReadOnlyCollection<Guid>>, IAuthorizedRequest
{
    // ADR-016: leitura = qualquer role canônica (Viewer, Operator, Admin).
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class ResolveNotificationUsersQueryHandler(
    INotificationRecipientRepository repository)
    : IRequestHandler<ResolveNotificationUsersQuery, IReadOnlyCollection<Guid>>
{
    public Task<IReadOnlyCollection<Guid>> Handle(
        ResolveNotificationUsersQuery request, CancellationToken cancellationToken)
        => repository.ResolveUserIdsAsync(request.NotificationId, cancellationToken);
}
