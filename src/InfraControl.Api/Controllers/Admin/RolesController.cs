using InfraControl.Domain.Enums;
using InfraControl.Application.Identity.Roles.Commands;
using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Admin;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InfraControl.Api.Authorization;

namespace InfraControl.Api.Controllers.Admin;

/// <summary>
/// F0-T023: erros (KeyNotFound/UnauthorizedAccess/InvalidOperation) são
/// embrulhados pelo GlobalExceptionHandler — o controller só compõe sucesso.
/// </summary>
// api/v1/admin/user-roles e não api/v1/admin/users: o UsersController já
// ocupa /admin/users/{userId}/roles/{role} e a rota idêntica aqui causaria
// AmbiguousMatchException no roteamento.
[Authorize(Policy = AuthorizationPolicies.Administer)]
[Route("api/v1/admin/user-roles")]
public sealed class RolesController(ISender sender) : BaseController
{
    [HttpPut("{userId:guid}/roles")]
    public async Task<IActionResult> Assign(Guid userId, [FromBody] RoleViewModel model, CancellationToken cancellationToken)
    {
        await sender.Send(new AssignUserRoleCommand(userId, model.Role), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{userId:guid}/roles/{role}")]
    public async Task<IActionResult> Remove(Guid userId, UserRole role, CancellationToken cancellationToken)
    {
        await sender.Send(new RemoveUserRoleCommand(userId, role), cancellationToken);
        return NoContent();
    }
}
