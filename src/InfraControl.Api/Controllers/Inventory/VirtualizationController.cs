using InfraControl.Application.Inventory.Virtualization.Queries;
using InfraControl.Api.Controllers;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SearchVirtualMachinesQuery(term, page, pageSize, locationId, rackId),
            cancellationToken)));

    [HttpGet("virtual-machines/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetVirtualMachine(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(new GetVirtualMachineQuery(id), cancellationToken)));

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
}
