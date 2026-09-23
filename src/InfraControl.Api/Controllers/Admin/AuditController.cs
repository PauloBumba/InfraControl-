using InfraControl.Application.Common.Audit.Queries;
using InfraControl.Api.Controllers;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Admin;

[Authorize(Policy = "AdminOnly")]
[Route("api/v1/admin/audit")]
public sealed class AuditController(ISender sender) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? action,
        [FromQuery] string? outcome,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new SearchAuditEntriesQuery(action, outcome, from, to, page, pageSize),
            cancellationToken);

        return Ok(ApiResponse.Ok(result));
    }
}
