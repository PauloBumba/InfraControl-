using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Incidents;
using InfraControl.Application.Incidents.Commands;
using InfraControl.Application.Incidents.Queries;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Incidents;

/// <summary>
/// F6-T003: incidentes — auto-abertura vem do avaliador (Alert Critical);
/// aqui: leitura, criação manual, acknowledge (docs/15) e troca de status.
/// Falhas (400/404/403) embrulhadas pelo GlobalExceptionHandler.
/// </summary>
[Route("api/v1/incidents")]
public sealed class IncidentsController(ISender sender) : BaseController
{
    [HttpGet]
    [Authorize(Policy = "IncidentsRead")]
    public async Task<IActionResult> Search(
        [FromQuery] Guid? deviceId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SearchIncidentsQuery(deviceId, status, page, pageSize),
            cancellationToken)));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "IncidentsRead")]
    public async Task<IActionResult> GetById(
        Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetIncidentQuery(id), cancellationToken)));

    [HttpPost]
    [Authorize(Policy = "IncidentsManage")]
    public async Task<IActionResult> Create(
        [FromBody] CreateIncidentViewModel model,
        CancellationToken cancellationToken)
    {
        var severity = ParseSeverity(model.Severity);
        var result = await sender.Send(
            new CreateIncidentCommand(model.Title, severity,
                model.Description, model.DeviceId),
            cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpPost("{id:guid}/acknowledge")]
    [Authorize(Policy = "IncidentsManage")]
    public async Task<IActionResult> Acknowledge(
        Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new AcknowledgeIncidentCommand(id), cancellationToken)));

    [HttpPut("{id:guid}/status")]
    [Authorize(Policy = "IncidentsManage")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateIncidentStatusViewModel model,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new UpdateIncidentStatusCommand(id, model.Status), cancellationToken)));

    private static IncidentSeverity ParseSeverity(string? severity)
    {
        if (string.IsNullOrWhiteSpace(severity)
            || !Enum.TryParse(severity.Trim(), ignoreCase: true, out IncidentSeverity parsed))
            throw new ArgumentException(
                $"Severidade inválida: '{severity}'. Use low, medium, high ou critical.",
                nameof(severity));

        return parsed;
    }
}
