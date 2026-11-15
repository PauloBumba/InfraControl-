using InfraControl.Application.Messaging.Explorer;
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
}

public sealed record MessagingQueuePauseRequest(string? Reason);
