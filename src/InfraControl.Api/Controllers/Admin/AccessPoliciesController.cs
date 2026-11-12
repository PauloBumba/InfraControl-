using InfraControl.Api.Authorization;
using InfraControl.Api.Controllers;
using InfraControl.Application.Common.Security;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Admin;

/// <summary>
/// Matriz de acesso dinâmico role×módulo (ADR-066): o admin define pela
/// plataforma o nível que cada role tem em cada módulo, sem mudar código.
/// Tabela vazia = defaults do ADR-016.
/// </summary>
[Route("api/v1/admin/access-policies")]
// F51 — GET aberto a autenticados (operadores veem a matriz); PUT segue
// Administer (a policy fica na action, não na classe).
[Authorize]
public sealed class AccessPoliciesController(ISender sender) : BaseController
{
    /// <summary>Matriz efetiva: módulos × roles (nível + flag de override).</summary>
    [HttpGet]
    public async Task<IActionResult> GetMatrix(CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetAccessPolicyMatrixQuery(), cancellationToken)));

    /// <summary>Substitui o conjunto de overrides (level null = volta ao default).</summary>
    [HttpPut]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> ReplaceGrants(
        [FromBody] ReplaceAccessPolicyGrantsRequest request,
        CancellationToken cancellationToken)
    {
        request ??= new ReplaceAccessPolicyGrantsRequest();
        return Ok(ApiResponse.Ok(await sender.Send(
            new UpdateAccessPolicyGrantsCommand(request.Grants ?? []),
            cancellationToken)));
    }
}

public sealed class ReplaceAccessPolicyGrantsRequest
{
    public List<AccessPolicyGrantInput>? Grants { get; set; }
}
