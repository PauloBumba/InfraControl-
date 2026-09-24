using InfraControl.Application.Identity.Administration;
using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Admin;
using InfraControl.BuildingBlocks.Application.Response;
using InfraControl.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Admin;

[Route("api/v1/admin/users")]
[Authorize(Roles = "Admin,User")]
public sealed class UsersController(IIdentityAdministrationService identityService) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await identityService.ListUsersAsync(cancellationToken)));

    [HttpPatch("{userId:guid}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetStatus(Guid userId, SetUserStatusViewModel model, CancellationToken cancellationToken)
        => await identityService.SetActiveAsync(userId, model.Active, cancellationToken)
            ? NoContent()
            : NotFound(ApiResponse<object>.Fail(["Usuário não encontrado."]));

    [HttpPost("{userId:guid}/roles/{role}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AssignRole(Guid userId, UserRole role, CancellationToken cancellationToken)
        => await identityService.AssignRoleAsync(userId, role, cancellationToken)
            ? NoContent()
            : NotFound(ApiResponse<object>.Fail(["Usuário não encontrado."]));

    [HttpDelete("{userId:guid}/roles/{role}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RemoveRole(Guid userId, UserRole role, CancellationToken cancellationToken)
        // F0-T023: "último admin" (InvalidOperation → 409) embrulhado pelo
        // GlobalExceptionHandler; aqui só o resultado compõe a resposta.
        => await identityService.RemoveRoleAsync(userId, role, cancellationToken)
            ? NoContent()
            : NotFound(ApiResponse<object>.Fail(["Usuário não encontrado."]));
}
