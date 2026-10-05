using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Notifications.Feed.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Notifications.Feed.Queries;

/// <summary>
/// ADR-030 §2 — GET /notifications/{id}: recuperação pontual com a MESMA
/// visibilidade do feed (destinatário direto OU de role via
/// <see cref="INotificationFeedRepository.GetForUserAsync"/>); fora da
/// visibilidade responde 404 — mesma resposta do inexistente, sem expor
/// notificação alheia.
/// </summary>
public sealed record GetNotificationQuery(Guid Id) : IQuery<NotificationFeedItemDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Read;
}

public sealed class GetNotificationQueryHandler(
    INotificationFeedRepository repository,
    ICurrentUserContext currentUser)
    : IRequestHandler<GetNotificationQuery, NotificationFeedItemDto>
{
    public async Task<NotificationFeedItemDto> Handle(
        GetNotificationQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException(
                "Usuário autenticado é obrigatório para o feed de notificações.");

        var notification = await repository.GetForUserAsync(
                request.Id, userId, currentUser.Roles, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Notificação '{request.Id}' não encontrada.");

        return new NotificationFeedItemDto(
            notification.Id,
            notification.EventType,
            notification.Title,
            notification.Message,
            notification.CreateDateTime);
    }
}
