using InfraControl.Api.Authorization;
using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Knowledge;
using InfraControl.Application.Knowledge.Articles;
using InfraControl.Application.Knowledge.Articles.Commands;
using InfraControl.Application.Knowledge.Articles.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.BuildingBlocks.Application.Response;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Knowledge;

/// <summary>
/// F10-T002: CRUD de artigos da base de conhecimento (docs/04).
/// Controller próprio — o de categorias (F10-T001) já ocupa a rota
/// api/v1/knowledge/categories. F10-T003 + K2: transições de status em
/// POST {id}/status. F10-T004: vínculos N:N em
/// POST/DELETE {id}/{devices|incidents|services}/{targetId}.
/// F24-T003/K3: histórico de versões em GET {id}/versions e
/// GET {id}/versions/compare?from=&to= (literal antes de {version}…
/// — não há rota {version}, o compare é o único sub-recurso).
/// Falhas embrulhadas pelo GlobalExceptionHandler.
/// </summary>
[Route("api/v1/knowledge/articles")]
public sealed class KnowledgeArticlesController(ISender sender) : BaseController
{
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Search(
        [FromQuery] string? term,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        // F24-T005/K5: "pendentes de revisão" — a action NÃO repassava
        // (bug de wiring pego na K7: o handler tinha o parâmetro e a
        // query string era ignorada). F24-T007/K7: filtros estruturados
        // (docs/32 §13) — "sistema"/"setor" não existem no modelo.
        [FromQuery] bool pendingReview = false,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] Guid? authorId = null,
        [FromQuery] KnowledgeArticleType? type = null,
        [FromQuery] string? tag = null,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SearchKnowledgeArticlesQuery(
                term, page, pageSize, pendingReview,
                categoryId, authorId, type, tag), cancellationToken)));

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(new GetKnowledgeArticleQuery(id), cancellationToken)));

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Create(
        [FromBody] CreateKnowledgeArticleViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateKnowledgeArticleCommand(
                model.Title, model.Content, model.CategoryId,
                model.Type, model.Description, model.Tags, model.ResponsibleId, model.ImagePath),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateKnowledgeArticleViewModel model,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateKnowledgeArticleCommand(
                id, model.Title, model.Content, model.CategoryId,
                model.Reason,
                model.Type, model.Description, model.Tags, model.ResponsibleId, model.ImagePath),
            cancellationToken);
        return NoContent();
    }

    // F24-T003/K3 (spec docs/32 §8): versões nunca perdidas — lista com
    // metadados e comparação devolvendo os dois snapshots (o diff é
    // decisão de quem consome). Mesma visibilidade do artigo.

    /// <summary>Histórico de versões, mais recente primeiro (Read).</summary>
    [HttpGet("{id:guid}/versions")]
    [Authorize]
    public async Task<IActionResult> ListVersions(
        Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new ListKnowledgeArticleVersionsQuery(id), cancellationToken)));

    /// <summary>Snapshot das duas versões lado a lado (Read) —
    /// 404 pt-BR se qualquer lado não existir.</summary>
    [HttpGet("{id:guid}/versions/compare")]
    [Authorize]
    public async Task<IActionResult> CompareVersions(
        Guid id,
        [FromQuery] string from,
        [FromQuery] string to,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new CompareKnowledgeArticleVersionsQuery(id, from, to),
            cancellationToken)));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteKnowledgeArticleCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>K2/ADR-034: workflow de 8 status (docs/32 §7);
    /// fora da matriz = 409 pt-BR, status desconhecido = 400 pt-BR.</summary>
    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> ChangeStatus(
        Guid id,
        [FromBody] ChangeKnowledgeArticleStatusViewModel model,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new ChangeKnowledgeArticleStatusCommand(id, model.Status), cancellationToken)));

    // F10-T004: vínculos N:N (Device/Incident/Service) — 204 nos dois
    // sentidos e estado atual embutido no dto do GET (deviceIds,
    // incidentIds, serviceIds).

    [HttpPost("{id:guid}/devices/{deviceId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> LinkDevice(
        Guid id,
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        await sender.Send(new LinkKnowledgeArticleDeviceCommand(id, deviceId), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}/devices/{deviceId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> UnlinkDevice(
        Guid id,
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        await sender.Send(new UnlinkKnowledgeArticleDeviceCommand(id, deviceId), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/incidents/{incidentId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> LinkIncident(
        Guid id,
        Guid incidentId,
        CancellationToken cancellationToken)
    {
        await sender.Send(new LinkKnowledgeArticleIncidentCommand(id, incidentId), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}/incidents/{incidentId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> UnlinkIncident(
        Guid id,
        Guid incidentId,
        CancellationToken cancellationToken)
    {
        await sender.Send(new UnlinkKnowledgeArticleIncidentCommand(id, incidentId), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/services/{serviceId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> LinkService(
        Guid id,
        Guid serviceId,
        CancellationToken cancellationToken)
    {
        await sender.Send(new LinkKnowledgeArticleServiceCommand(id, serviceId), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}/services/{serviceId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> UnlinkService(
        Guid id,
        Guid serviceId,
        CancellationToken cancellationToken)
    {
        await sender.Send(new UnlinkKnowledgeArticleServiceCommand(id, serviceId), cancellationToken);
        return NoContent();
    }

    // F24-T004/K4: origens polimórficas (docs/32 §9) — Task/PDI/
    // Chamado. O tipo é string na rota e validado no handler (400
    // pt-BR com as opções); alvo inexistente = 404 pt-BR; duplicado
    // = 409. Vem embutido em `origins` no GET (detalhe e busca).

    [HttpPost("{id:guid}/origins/{type}/{originId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> LinkOrigin(
        Guid id,
        string type,
        Guid originId,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new LinkKnowledgeArticleOriginCommand(id, type, originId),
            cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}/origins/{type}/{originId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> UnlinkOrigin(
        Guid id,
        string type,
        Guid originId,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UnlinkKnowledgeArticleOriginCommand(id, type, originId),
            cancellationToken);
        return NoContent();
    }

    // F24-T005/K5: revisão periódica (docs/32 §6 governança + §12).
    // Agenda à parte do PUT do artigo; "manter" = reconfirmar. As
    // outras ações da §12 já existem: "atualizar" = PUT (reagenda) e
    // "arquivar" = POST {id}/status (ADR-034).

    /// <summary>Configura/limpa a agenda de revisão (intervalo em dias +
    /// responsável) — 400 pt-BR fora de 1..3650 (Operate).</summary>
    [HttpPut("{id:guid}/review-schedule")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> SetReviewSchedule(
        Guid id,
        [FromBody] SetKnowledgeArticleReviewScheduleViewModel model,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new SetKnowledgeArticleReviewScheduleCommand(
                id, model.IntervalDays, model.ReviewResponsibleId),
            cancellationToken);
        return NoContent();
    }

    /// <summary>Ação "manter" da §12: revisão feita sem mudar o conteúdo
    /// → reagenda (409 sem agenda configurada) (Operate).</summary>
    [HttpPost("{id:guid}/review/confirm")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> ConfirmReview(
        Guid id,
        CancellationToken cancellationToken)
    {
        await sender.Send(new ConfirmKnowledgeArticleReviewCommand(id), cancellationToken);
        return NoContent();
    }
}
