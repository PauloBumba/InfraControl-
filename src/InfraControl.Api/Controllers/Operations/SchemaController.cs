using InfraControl.Application.Operations.Schema;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InfraControl.Api.Authorization;

namespace InfraControl.Api.Controllers.Operations;

/// <summary>
/// Dicionário de dados dinâmico: entidades, tabelas, colunas e
/// relacionamentos (FKs) lidos do modelo EF — Administer.
/// </summary>
[Route("api/v1/operations/schema")]
[Authorize(Policy = AuthorizationPolicies.Administer)]
public sealed class SchemaController(ISender sender) : BaseController
{
    /// <summary>Todas as entidades mapeadas com colunas e alvos de FK.</summary>
    [HttpGet]
    public async Task<IActionResult> GetCatalog(CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetSchemaCatalogQuery(), cancellationToken)));
}
