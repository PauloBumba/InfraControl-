using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Notifications;
using InfraControl.Application.Notifications.Feed.Queries;
using InfraControl.Application.Notifications.Preferences.Commands;
using InfraControl.Application.Notifications.Preferences.Queries;
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
    /// <summary>
    /// F18-T001 — feed do usuário autenticado: destinatário direto ou de
    /// role, mais recentes primeiro. DTO sem estado de leitura (decisão
    /// da task — a entidade não tem flag de lida).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetFeed(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetNotificationFeedQuery(page, pageSize),
            cancellationToken)));

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

    // F9-T003: preferências de notificação por usuário/canal.

    [HttpGet("preferences")]
    public async Task<IActionResult> SearchPreferences(
        [FromQuery] Guid userId,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SearchNotificationPreferencesQuery(userId), cancellationToken)));

    /// <summary>Upsert: cria ou atualiza a preferência (usuário, canal).</summary>
    [HttpPut("preferences")]
    public async Task<IActionResult> SetPreference(
        [FromBody] SetNotificationPreferenceViewModel model,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SetNotificationPreferenceCommand(
                model.UserId, model.Channel, model.Enabled),
            cancellationToken)));

    [HttpDelete("preferences")]
    public async Task<IActionResult> DeletePreference(
        [FromQuery] Guid userId,
        [FromQuery] Domain.Enums.NotificationChannel channel,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new DeleteNotificationPreferenceCommand(userId, channel), cancellationToken);
        return NoContent();
    }
}
