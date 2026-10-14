using InfraControl.Application.Network.SwitchPorts.Commands;
using InfraControl.Application.Network.SwitchPorts.Queries;
using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Network;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InfraControl.Api.Authorization;

namespace InfraControl.Api.Controllers.Network;

/// <summary>
/// F3-T002: portas dos switches. Coleção sob o switch
/// (network/switches/{id}/ports), item em network/switch-ports/{id}.
/// Guard de type=Switch é reusado do F3-T001 no handler (404 pt-BR);
/// falhas embrulhadas pelo GlobalExceptionHandler.
/// </summary>
[Route("api/v1/network")]
public sealed class SwitchPortsController(ISender sender) : BaseController
{
    [HttpGet("switches/{switchId:guid}/ports")]
    [Authorize]
    public async Task<IActionResult> Search(
        Guid switchId,
        [FromQuery] string? term,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SearchSwitchPortsQuery(switchId, term, page, pageSize), cancellationToken)));

    /// <summary>
    /// Fase 07a: portas UniFi ao vivo do switch (port_table via
    /// ExternalDeviceReference). Lista vazia = sem referência UniFi ou
    /// device não encontrado no controller — não é erro.
    /// </summary>
    [HttpGet("switches/{switchId:guid}/unifi-ports")]
    [Authorize]
    public async Task<IActionResult> GetUnifiPorts(
        Guid switchId, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetSwitchUnifiPortsQuery(switchId), cancellationToken)));

    /// <summary>
    /// Fase 07b: interfaces SNMP monitoradas pelo Zabbix para o switch
    /// (via referência Zabbix). Lista vazia = sem referência ou Zabbix
    /// não configurado — não é erro.
    /// </summary>
    [HttpGet("switches/{switchId:guid}/zabbix-interfaces")]
    [Authorize]
    public async Task<IActionResult> GetZabbixInterfaces(
        Guid switchId, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetSwitchZabbixInterfacesQuery(switchId), cancellationToken)));

    /// <summary>
    /// Fase 07c: sincroniza o cadastro de portas a partir do port_table
    /// do controller UniFi. Portas existentes têm LinkUp atualizado;
    /// novas são criadas; manuais ausentes do controller são preservadas.
    /// Exige referência UniFi — 409 se ausente.
    /// </summary>
    [HttpPost("switches/{switchId:guid}/sync-unifi")]
    [Authorize(Policy = "Operate")]
    public async Task<IActionResult> SyncFromUnifi(
        Guid switchId, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SyncSwitchPortsFromUnifiCommand(switchId), cancellationToken)));

    [HttpPost("switches/{switchId:guid}/ports")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Create(
        Guid switchId,
        [FromBody] CreateSwitchPortViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateSwitchPortCommand(switchId, model.Name, model.Description),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpGet("switch-ports/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(new GetSwitchPortQuery(id), cancellationToken)));

    [HttpPut("switch-ports/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateSwitchPortViewModel model,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateSwitchPortCommand(id, model.Name, model.Description, model.LinkUp),
            cancellationToken);
        return NoContent();
    }

    [HttpDelete("switch-ports/{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteSwitchPortCommand(id), cancellationToken);
        return NoContent();
    }
}
