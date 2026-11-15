using InfraControl.Application.Messaging.Explorer;
using InfraControl.Application.Messaging.Failures;
using InfraControl.Application.Messaging.Overview;
using InfraControl.Application.Messaging.Workers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Messaging;

/// <summary>
/// F55: Messaging Operations Center — leituras nível Operate
/// (AuthorizationBehavior), escritas Administer nas tasks de gestão.
/// </summary>
[Route("api/v1/messaging")]
public sealed class MessagingController(ISender sender) : BaseController
{
    [HttpGet("overview")]
    [Authorize]
    public async Task<IActionResult> GetOverview(CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetMessagingOverviewQuery(), cancellationToken)));

    [HttpGet("queues")]
    [Authorize]
    public async Task<IActionResult> GetQueues(CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetMessagingQueuesQuery(), cancellationToken)));

    [HttpGet("queues/{name}")]
    [Authorize]
    public async Task<IActionResult> GetQueue(
        string name, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetMessagingQueueDetailQuery(name), cancellationToken)));

    [HttpGet("exchanges")]
    [Authorize]
    public async Task<IActionResult> GetExchanges(CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetMessagingExchangesQuery(), cancellationToken)));

    [HttpGet("bindings")]
    [Authorize]
    public async Task<IActionResult> GetBindings(CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetMessagingBindingsQuery(), cancellationToken)));

    [HttpGet("connections")]
    [Authorize]
    public async Task<IActionResult> GetConnections(CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetMessagingConnectionsQuery(), cancellationToken)));

    /// <summary>F55-T003: workers = containers da app × consumers do broker.</summary>
    [HttpGet("workers")]
    [Authorize]
    public async Task<IActionResult> GetWorkers(CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetMessagingWorkersQuery(), cancellationToken)));

    /// <summary>Pausa o consumo de uma fila (Administer, auditado).</summary>
    [HttpPost("queues/{name}/pause")]
    [Authorize]
    public async Task<IActionResult> PauseQueue(
        string name, [FromBody] MessagingQueuePauseRequest? body,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new PauseQueueConsumptionCommand(name, body?.Reason), cancellationToken)));

    /// <summary>Retoma o consumo (recicla as conexões gravadas no pause).</summary>
    [HttpPost("queues/{name}/resume")]
    [Authorize]
    public async Task<IActionResult> ResumeQueue(
        string name, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new ResumeQueueConsumptionCommand(name), cancellationToken)));

    // ---- F55-T004: Message Explorer -------------------------------------

    [HttpGet("messages")]
    [Authorize]
    public async Task<IActionResult> SearchMessages(
        [FromQuery] Guid? messageId, [FromQuery] string? correlationId,
        [FromQuery] string? eventType, [FromQuery] string? state,
        [FromQuery] string? text, [FromQuery] int? minAttempts,
        [FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(new SearchMessagingMessagesQuery(
            messageId, correlationId, eventType, state, text, minAttempts,
            fromUtc, toUtc,
            Math.Max(1, page), Math.Clamp(pageSize, 1, 100)), cancellationToken)));

    [HttpGet("messages/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetMessage(
        Guid id, CancellationToken cancellationToken)
    {
        var detail = await sender.Send(
            new GetMessagingMessageDetailQuery(id), cancellationToken);
        return detail is null
            ? NotFound(ApiResponse.Fail(["Mensagem não encontrada."]))
            : Ok(ApiResponse.Ok(detail));
    }

    [HttpGet("deliveries")]
    [Authorize]
    public async Task<IActionResult> SearchDeliveries(
        [FromQuery] Guid? deliveryId, [FromQuery] Guid? notificationId,
        [FromQuery] string? channel, [FromQuery] string? status,
        [FromQuery] int? minAttempts,
        [FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(new SearchMessagingDeliveriesQuery(
            deliveryId, notificationId, channel, status, minAttempts,
            fromUtc, toUtc,
            Math.Max(1, page), Math.Clamp(pageSize, 1, 100)), cancellationToken)));

    // ---- F55-T005: Retry + DLQ + replay ---------------------------------

    [HttpGet("retries")]
    [Authorize]
    public async Task<IActionResult> SearchRetries(
        [FromQuery] Guid? deliveryId, [FromQuery] Guid? notificationId,
        [FromQuery] string? channel, [FromQuery] int? minAttempts,
        [FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(new SearchMessagingRetriesQuery(
            deliveryId, notificationId, channel, minAttempts, fromUtc, toUtc,
            Math.Max(1, page), Math.Clamp(pageSize, 1, 100)), cancellationToken)));

    [HttpGet("dlq")]
    [Authorize]
    public async Task<IActionResult> SearchDlq(
        [FromQuery] Guid? deliveryId, [FromQuery] Guid? notificationId,
        [FromQuery] string? channel, [FromQuery] int? minAttempts,
        [FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(new SearchMessagingDlqQuery(
            deliveryId, notificationId, channel, minAttempts, fromUtc, toUtc,
            Math.Max(1, page), Math.Clamp(pageSize, 1, 100)), cancellationToken)));

    /// <summary>Replay seletivo de UMA delivery Failed (Administer, auditado).</summary>
    [HttpPost("deliveries/{id:guid}/replay")]
    [Authorize]
    public async Task<IActionResult> ReplayDelivery(
        Guid id, [FromBody] MessagingReplayRequest? body,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new ReplayNotificationDeliveryCommand(id, body?.Reason), cancellationToken)));
}

public sealed record MessagingQueuePauseRequest(string? Reason);

public sealed record MessagingReplayRequest(string? Reason);
