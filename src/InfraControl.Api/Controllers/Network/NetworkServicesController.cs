using InfraControl.Api.Authorization;
using InfraControl.Api.Controllers;
using InfraControl.Application.Network.Services.Queries;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Network;

/// <summary>
/// F18-T005 (ADR-042): leitura dos serviços de rede prometidos pelas
/// abas removidas no F17-T008 — leases DHCP/ARP (SNMP), rotas (SNMP) e
/// regras de firewall (SSH por vendor). SOMENTE LEITURA: nenhum endpoint
/// desta rota escreve no equipamento. Sem equipamento configurado ou
/// inalcançável = 503 com o motivo no envelope (mesma semântica da
/// integração UniFi — nunca o 409 do handler global). Leitura de
/// operação: só autenticado, roles no IAuthorizedRequest (nível único
/// ADR-030 §3).
/// </summary>
[Authorize]
[Route("api/v1/network/services")]
public sealed class NetworkServicesController(ISender sender) : BaseController
{
    /// <summary>Leases/ARP ativos do equipamento (ipNetToMedia/ipNetToPhysical).</summary>
    [HttpGet("dhcp/leases")]
    public async Task<IActionResult> DhcpLeases(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(ApiResponse.Ok(await sender.Send(
                new GetDhcpLeasesQuery(), cancellationToken)));
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or UnauthorizedAccessException)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                ApiResponse.Fail([exception.Message]));
        }
    }

    /// <summary>Tabela de rotas IP do equipamento (ipRouteTable, RFC1213).</summary>
    [HttpGet("routes")]
    public async Task<IActionResult> Routes(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(ApiResponse.Ok(await sender.Send(
                new GetRoutesQuery(), cancellationToken)));
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or UnauthorizedAccessException)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                ApiResponse.Fail([exception.Message]));
        }
    }

    /// <summary>Regras de firewall (ACLs) — comando show de leitura por vendor.</summary>
    [HttpGet("firewall/rules")]
    public async Task<IActionResult> FirewallRules(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(ApiResponse.Ok(await sender.Send(
                new GetFirewallRulesQuery(), cancellationToken)));
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or UnauthorizedAccessException)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                ApiResponse.Fail([exception.Message]));
        }
    }
}
