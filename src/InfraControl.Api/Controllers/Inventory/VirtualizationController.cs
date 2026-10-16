using InfraControl.Application.Inventory.Virtualization.Queries;
using InfraControl.Application.Inventory.Virtualization.Commands;
using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Inventory;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InfraControl.Api.Authorization;

namespace InfraControl.Api.Controllers.Inventory;

/// <summary>
/// F31-T001 (docs/53 §4.1): visão de VMs e vCenter reusando o fluxo de Device
/// (F2-T003) com type fixo VirtualMachine/VCenter (D2). Falhas (400/404/401/403)
/// embrulhadas pelo GlobalExceptionHandler; vínculos VM→hypervisor→vCenter na
/// F31-T002.
/// </summary>
[Route("api/v1/inventory")]
public sealed class VirtualizationController(ISender sender) : BaseController
{
    [HttpGet("virtual-machines")]
    [Authorize]
    public async Task<IActionResult> SearchVirtualMachines(
        [FromQuery] string? term,
        [FromQuery] Guid? locationId,
        [FromQuery] Guid? rackId,
        [FromQuery] Guid? hypervisorId,
        [FromQuery] Guid? vCenterId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SearchVirtualMachinesQuery(
                term, page, pageSize, locationId, rackId, hypervisorId, vCenterId),
            cancellationToken)));

    [HttpGet("virtual-machines/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetVirtualMachine(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(new GetVirtualMachineQuery(id), cancellationToken)));

    // F31-T002: vínculos VM → hypervisor → vCenter (FK self em devices).
    // Assign devolve a VM atualizada (201); unassign responde 204.
    [HttpPost("virtual-machines/{virtualMachineId:guid}/hypervisor")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> AssignHypervisor(
        Guid virtualMachineId,
        [FromBody] AssignHypervisorViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new AssignHypervisorToVirtualMachineCommand(virtualMachineId, model.HypervisorId),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpDelete("virtual-machines/{virtualMachineId:guid}/hypervisor")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> UnassignHypervisor(
        Guid virtualMachineId,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UnassignHypervisorFromVirtualMachineCommand(virtualMachineId),
            cancellationToken);
        return NoContent();
    }

    [HttpPost("virtual-machines/{virtualMachineId:guid}/vcenter")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> AssignVCenter(
        Guid virtualMachineId,
        [FromBody] AssignVCenterViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new AssignVCenterToVirtualMachineCommand(virtualMachineId, model.VCenterId),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpDelete("virtual-machines/{virtualMachineId:guid}/vcenter")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> UnassignVCenter(
        Guid virtualMachineId,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UnassignVCenterFromVirtualMachineCommand(virtualMachineId),
            cancellationToken);
        return NoContent();
    }

    [HttpGet("vcenters")]
    [Authorize]
    public async Task<IActionResult> SearchVCenters(
        [FromQuery] string? term,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SearchVCentersQuery(term, page, pageSize),
            cancellationToken)));

    [HttpGet("vcenters/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetVCenter(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(new GetVCenterQuery(id), cancellationToken)));

    // F31-T003: visão agregada (contagens + órfãs + nomes resolvidos).
    [HttpGet("virtualization")]
    [Authorize]
    public async Task<IActionResult> GetOverview(CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetVirtualizationOverviewQuery(), cancellationToken)));
}
