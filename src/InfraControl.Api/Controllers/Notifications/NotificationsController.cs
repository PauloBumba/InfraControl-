using InfraControl.Api.Controllers;
using InfraControl.Application.Notifications.Recipients.Queries;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Notifications;

/// <summary>
/// Destinatários das notificações (F9-T002, docs/07) — leitura pura;
/// criação acontece no Notification Consumer (domain events).
/// </summary>
[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public sealed class NotificationsController(IMediator sender) : BaseController
{
    [HttpGet("{notificationId:guid}/recipients")]
    public async Task<IActionResult> GetRecipients(
        Guid notificationId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetNotificationRecipientsQuery(notificationId, page, pageSize),
            cancellationToken)));

    /// <summary>
    /// Usuários finais únicos (roles resolvidas + dedup) — consumido pela
    /// entrega nos canais (F9-T004+).
    /// </summary>
    [HttpGet("{notificationId:guid}/recipients/users")]
    public async Task<IActionResult> ResolveUsers(
        Guid notificationId, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new ResolveNotificationUsersQuery(notificationId), cancellationToken)));
}
