using InfraControl.Api.Authorization;
using InfraControl.Api.Controllers;
using InfraControl.Application.Common.Security;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Admin;

/// <summary>
/// Perfis de acesso customizados (spec evolução): o admin cria papéis
/// nomeados e define o que cada um pode ver/fazer na matriz de
/// <c>/admin/access-policies</c>, depois atribui aos usuários.
/// Somente Admin.
/// </summary>
[Route("api/v1/admin/access-roles")]
[Authorize(Policy = AuthorizationPolicies.Administer)]
public sealed class AccessRolesController(ISender sender) : BaseController
{
    /// <summary>Lista perfis customizados com contagem de usuários.</summary>
    [HttpGet]
    public async Task<IActionResult> Search(CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SearchAccessRolesQuery(), cancellationToken)));

    /// <summary>Cria um perfil (key = slug estável, imutável).</summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] UpsertAccessRoleRequest request,
        CancellationToken cancellationToken)
    {
        request ??= new UpsertAccessRoleRequest();
        var result = await sender.Send(
            new CreateAccessRoleCommand(request.Key, request.Label, request.Description),
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    /// <summary>Atualiza rótulo/descrição (a chave não muda).</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpsertAccessRoleRequest request,
        CancellationToken cancellationToken)
    {
        request ??= new UpsertAccessRoleRequest();
        return Ok(ApiResponse.Ok(await sender.Send(
            new UpdateAccessRoleCommand(id, request.Label, request.Description),
            cancellationToken)));
    }

    /// <summary>Remove o perfil, suas atribuições e suas regras de módulo.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteAccessRoleCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>Perfis customizados atribuídos a um usuário.</summary>
    [HttpGet("users/{userId:guid}")]
    public async Task<IActionResult> GetUserRoles(Guid userId, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetUserAccessRolesQuery(userId), cancellationToken)));

    /// <summary>Substitui o conjunto de perfis customizados do usuário.</summary>
    [HttpPut("users/{userId:guid}")]
    public async Task<IActionResult> SetUserRoles(
        Guid userId,
        [FromBody] SetUserAccessRolesRequest request,
        CancellationToken cancellationToken)
    {
        request ??= new SetUserAccessRolesRequest();
        return Ok(ApiResponse.Ok(await sender.Send(
            new SetUserAccessRolesCommand(userId, request.RoleKeys ?? []),
            cancellationToken)));
    }
}

public sealed class UpsertAccessRoleRequest
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public sealed class SetUserAccessRolesRequest
{
    public List<string>? RoleKeys { get; set; }
}
