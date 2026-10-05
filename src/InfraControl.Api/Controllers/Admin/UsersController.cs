using InfraControl.Application.Identity.Administration;
using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Admin;
using InfraControl.BuildingBlocks.Application;
using InfraControl.BuildingBlocks.Application.Response;
using InfraControl.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InfraControl.Api.Authorization;

namespace InfraControl.Api.Controllers.Admin;

[Route("api/v1/admin/users")]
[Authorize(Policy = AuthorizationPolicies.Operate)]
public sealed class UsersController(
    IIdentityAdministrationService identityService,
    ICurrentUserContext currentUserContext) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await identityService.ListUsersAsync(cancellationToken)));

    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> Get(Guid userId, CancellationToken cancellationToken)
    {
        var summary = await identityService.GetUserAsync(userId, cancellationToken);
        return summary is null
            ? NotFound(ApiResponse<object>.Fail(["Usuário não encontrado."]))
            : Ok(ApiResponse.Ok(summary));
    }

    [HttpPatch("{userId:guid}/status")]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> SetStatus(Guid userId, SetUserStatusViewModel model, CancellationToken cancellationToken)
        => await identityService.SetActiveAsync(userId, model.Active, cancellationToken)
            ? NoContent()
            : NotFound(ApiResponse<object>.Fail(["Usuário não encontrado."]));

    [HttpPost("{userId:guid}/roles/{role}")]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> AssignRole(Guid userId, UserRole role, CancellationToken cancellationToken)
        => await identityService.AssignRoleAsync(userId, role, cancellationToken)
            ? NoContent()
            : NotFound(ApiResponse<object>.Fail(["Usuário não encontrado."]));

    [HttpDelete("{userId:guid}/roles/{role}")]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> RemoveRole(Guid userId, UserRole role, CancellationToken cancellationToken)
        // F0-T023: "último admin" (InvalidOperation → 409) embrulhado pelo
        // GlobalExceptionHandler; aqui só o resultado compõe a resposta.
        => await identityService.RemoveRoleAsync(userId, role, cancellationToken)
            ? NoContent()
            : NotFound(ApiResponse<object>.Fail(["Usuário não encontrado."]));

    [HttpPut("{userId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> Update(Guid userId, UpdateUserViewModel model, CancellationToken cancellationToken)
        => await identityService.UpdateUserAsync(userId, model.DisplayName, model.Email, cancellationToken)
            ? NoContent()
            : NotFound(ApiResponse<object>.Fail(["Usuário não encontrado."]));

    [HttpDelete("{userId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> Delete(Guid userId, CancellationToken cancellationToken)
    {
        if (currentUserContext.UserId is null)
            return Unauthorized(ApiResponse<object>.Fail(["Usuário não autenticado."]));

        return await identityService.DeleteUserAsync(userId, currentUserContext.UserId.Value, cancellationToken)
            ? NoContent()
            : NotFound(ApiResponse<object>.Fail(["Usuário não encontrado."]));
    }
}
