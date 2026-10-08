using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Sla;
using InfraControl.Api.ViewModels.Tasks;
using InfraControl.BuildingBlocks.Application.Response;
using InfraControl.Application.Knowledge.Articles.Commands;
using InfraControl.Application.Sla.TaskSlas.Commands;
using InfraControl.Application.Sla.TaskSlas.Queries;
using InfraControl.Application.Tasks.Commands;
using InfraControl.Application.Tasks.History;
using InfraControl.Application.Tasks.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InfraControl.Api.Authorization;

namespace InfraControl.Api.Controllers.Tasks;

/// <summary>
/// F7-T001: CRUD de Tasks (docs/04). Falhas (400/404/401/403) embrulhadas
/// pelo GlobalExceptionHandler; estado/transição = F7-T007 (Workflow).
/// </summary>
[Route("api/v1/tasks")]
public sealed class TasksController(ISender sender) : BaseController
{
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Search(
        [FromQuery] string? term,
        [FromQuery] string? kind,
        [FromQuery] string? state,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        // F23-T004/P4a — hierarquia: filhos de `parentId`; `rootOnly`
        // só tasks sem pai (exclusivos entre si → 400 no handler).
        [FromQuery] Guid? parentId = null,
        [FromQuery] bool rootOnly = false,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SearchTasksQuery(
                term, kind, state, page, pageSize, parentId, rootOnly),
            cancellationToken)));

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(
        Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(new GetTaskQuery(id), cancellationToken)));

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTaskViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateTaskCommand(
                model.Title,
                model.Kind,
                model.Description,
                model.Priority,
                model.DueDate,
                model.ResponsibleId,
                model.ParentTaskId),
            cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    // F23-T001: identificação básica (prioridade/prazo/responsável) —
    // substituição dos 3 campos; null limpa prazo/responsável; 400 com
    // prioridade vazia/inválida e 404 com responsável inexistente.

    [HttpPut("{id:guid}/identification")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> SetIdentification(
        Guid id,
        [FromBody] SetTaskIdentificationViewModel model,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SetTaskIdentificationCommand(
                id, model.Priority, model.DueDate, model.ResponsibleId),
            cancellationToken)));

    // F23-T004/P4a — subtarefas: move/desvincula o pai; `parentId`
    // nulo desvincula; auto-relação ou pai que é subtarefa → 400 pt-BR.

    [HttpPut("{id:guid}/parent")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> SetParent(
        Guid id,
        [FromBody] SetTaskParentViewModel model,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new SetTaskParentCommand(id, model.ParentId),
            cancellationToken)));

    // F23-T003: planejamento e resultado (§5) + replanejamento (§13) —
    // substituição com nulls limpando; fim < início ou motivo vazio →
    // 400 pt-BR; sem mudança real → 400 (nada a replanejar, no replan).

    [HttpPut("{id:guid}/planning")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> SetPlanning(
        Guid id,
        [FromBody] UpdateTaskPlanningViewModel model,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new UpdateTaskPlanningCommand(
                id, model.PlannedStart, model.PlannedEnd),
            cancellationToken)));

    [HttpPut("{id:guid}/result")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> SetResult(
        Guid id,
        [FromBody] UpdateTaskResultViewModel model,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new UpdateTaskResultCommand(id, model.Result),
            cancellationToken)));

    [HttpPost("{id:guid}/replan")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Replan(
        Guid id,
        [FromBody] ReplanTaskViewModel model,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new ReplanTaskCommand(
                id, model.PlannedStart, model.PlannedEnd,
                model.DueDate, model.Reason),
            cancellationToken)));

    // F24-T006/K6 (docs/32 §3, ADR-035): rascunho Knowledge montado dos
    // registros da task concluída — sempre nasce Draft → revisão.
    /// <summary>Gera rascunho a partir da task concluída — 409 se não
    /// estiver concluída (Operate).</summary>
    [HttpPost("{taskId:guid}/generate-knowledge")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> GenerateKnowledge(
        Guid taskId, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GenerateTaskKnowledgeCommand(taskId), cancellationToken)));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateTaskViewModel model,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateTaskCommand(id, model.Title, model.Description),
            cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Delete(
        Guid id, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteTaskCommand(id), cancellationToken);
        return NoContent();
    }

    // F7-T007 (Workflow): transição de estado — mapa no domínio
    // (409 fora do mapa), 400 estado desconhecido; mesmo estado = no-op
    // sem histórico. Retorna o TaskDto atualizado.

    [HttpPut("{id:guid}/state")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> ChangeState(
        Guid id,
        [FromBody] ChangeTaskStateViewModel model,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new ChangeTaskStateCommand(id, model.State), cancellationToken)));


    // F7-T002: responsáveis (N:N com usuários) — 204 nos dois sentidos
    // e estado atual embutido no TaskDto (assigneeIds).

    [HttpPost("{taskId:guid}/assignees/{userId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Assign(
        Guid taskId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await sender.Send(new AssignTaskCommand(taskId, userId), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{taskId:guid}/assignees/{userId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> Unassign(
        Guid taskId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await sender.Send(new UnassignTaskCommand(taskId, userId), cancellationToken);
        return NoContent();
    }

    // F7-T003: devices vinculados (N:N) — 204 nos dois sentidos e estado
    // atual embutido no TaskDto (deviceIds).

    [HttpPost("{taskId:guid}/devices/{deviceId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> LinkDevice(
        Guid taskId,
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        await sender.Send(new LinkTaskDeviceCommand(taskId, deviceId), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{taskId:guid}/devices/{deviceId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> UnlinkDevice(
        Guid taskId,
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        await sender.Send(new UnlinkTaskDeviceCommand(taskId, deviceId), cancellationToken);
        return NoContent();
    }

    // F7-T004: incidentes vinculados (N:N) — 204 nos dois sentidos e estado
    // atual embutido no TaskDto (incidentIds).

    [HttpPost("{taskId:guid}/incidents/{incidentId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> LinkIncident(
        Guid taskId,
        Guid incidentId,
        CancellationToken cancellationToken)
    {
        await sender.Send(new LinkTaskIncidentCommand(taskId, incidentId), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{taskId:guid}/incidents/{incidentId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> UnlinkIncident(
        Guid taskId,
        Guid incidentId,
        CancellationToken cancellationToken)
    {
        await sender.Send(new UnlinkTaskIncidentCommand(taskId, incidentId), cancellationToken);
        return NoContent();
    }

    // F23-T005/P5a: Knowledge vinculada (N:N com artigo, docs/32 §10) —
    // 204 nos dois sentidos e articleIds embutido no TaskDto.

    [HttpPost("{taskId:guid}/articles/{articleId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> LinkArticle(
        Guid taskId,
        Guid articleId,
        CancellationToken cancellationToken)
    {
        await sender.Send(new LinkTaskArticleCommand(taskId, articleId), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{taskId:guid}/articles/{articleId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> UnlinkArticle(
        Guid taskId,
        Guid articleId,
        CancellationToken cancellationToken)
    {
        await sender.Send(new UnlinkTaskArticleCommand(taskId, articleId), cancellationToken);
        return NoContent();
    }

    // F23-T005/P5b: PDI vinculado (N:N por dono do plano — 1 PDI por
    // usuário, spec §15) — 204 nos dois sentidos; pdiUserIds no TaskDto.

    [HttpPost("{taskId:guid}/pdi/{userId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> LinkPdi(
        Guid taskId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await sender.Send(new LinkTaskPdiCommand(taskId, userId), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{taskId:guid}/pdi/{userId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> UnlinkPdi(
        Guid taskId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await sender.Send(new UnlinkTaskPdiCommand(taskId, userId), cancellationToken);
        return NoContent();
    }

    // F7-T005: comentários — autor é o usuário logado (ICurrentUserContext).

    [HttpGet("{taskId:guid}/comments")]
    [Authorize]
    public async Task<IActionResult> GetComments(
        Guid taskId, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetTaskCommentsQuery(taskId), cancellationToken)));

    [HttpPut("comments/{commentId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> UpdateComment(
        Guid commentId,
        [FromBody] UpdateTaskCommentViewModel model,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new UpdateTaskCommentCommand(commentId, model.Content),
            cancellationToken)));

    [HttpDelete("comments/{commentId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> DeleteComment(
        Guid commentId, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteTaskCommentCommand(commentId), cancellationToken);
        return NoContent();
    }

    [HttpGet("{taskId:guid}/history")]
    [Authorize]
    public async Task<IActionResult> GetHistory(
        Guid taskId, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetTaskHistoryQuery(taskId), cancellationToken)));

    [HttpPost("{taskId:guid}/comments")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> AddComment(
        Guid taskId,
        [FromBody] AddTaskCommentViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new AddTaskCommentCommand(taskId, model.Content), cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    // F23-T003: critérios de aceite (§12) — leitura = [Authorize],
    // escrita = Operate; mesmas rotas/verbos dos comentários.

    [HttpGet("{taskId:guid}/criteria")]
    [Authorize]
    public async Task<IActionResult> GetCriteria(
        Guid taskId, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetTaskCriteriaQuery(taskId), cancellationToken)));

    [HttpPost("{taskId:guid}/criteria")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> AddCriterion(
        Guid taskId,
        [FromBody] AddTaskCriterionViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new AddTaskCriterionCommand(taskId, model.Description),
            cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpPut("criteria/{criterionId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> UpdateCriterion(
        Guid criterionId,
        [FromBody] UpdateTaskCriterionViewModel model,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new UpdateTaskCriterionCommand(
                criterionId, model.Description, model.Done),
            cancellationToken)));

    [HttpDelete("criteria/{criterionId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> DeleteCriterion(
        Guid criterionId, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteTaskCriterionCommand(criterionId), cancellationToken);
        return NoContent();
    }

    // F23-T004/P4b: impedimentos estruturados (§10) — leitura = [Authorize],
    // escrita = Operate; mesmas rotas/verbos dos critérios. Não mexe no
    // estado do workflow (Blocked segue independente — ADR exigiria).

    [HttpGet("{taskId:guid}/impediments")]
    [Authorize]
    public async Task<IActionResult> GetImpediments(
        Guid taskId, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetTaskImpedimentsQuery(taskId), cancellationToken)));

    [HttpPost("{taskId:guid}/impediments")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> AddImpediment(
        Guid taskId,
        [FromBody] AddTaskImpedimentViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new AddTaskImpedimentCommand(taskId, model.Description),
            cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpPut("impediments/{impedimentId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> UpdateImpediment(
        Guid impedimentId,
        [FromBody] UpdateTaskImpedimentViewModel model,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new UpdateTaskImpedimentCommand(
                impedimentId, model.Description, model.Resolved),
            cancellationToken)));

    [HttpDelete("impediments/{impedimentId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> DeleteImpediment(
        Guid impedimentId, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteTaskImpedimentCommand(impedimentId), cancellationToken);
        return NoContent();
    }

    // F8-T003: SLA aplicado — snapshot dos prazos (docs/04); 409 se já
    // aplicado, 404 task/política.

    [HttpGet("{taskId:guid}/sla")]
    [Authorize]
    public async Task<IActionResult> GetSla(
        Guid taskId, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetTaskSlaQuery(taskId), cancellationToken)));

    [HttpPost("{taskId:guid}/sla")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> ApplySla(
        Guid taskId,
        [FromBody] ApplyTaskSlaViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new ApplyTaskSlaCommand(taskId, model.SlaPolicyId), cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    // F8-T004: pausas do relógio de SLA — pausa/resume estendem os
    // vencimentos ao fechar (snapshot de minutos intacto).

    [HttpGet("{taskId:guid}/sla/pauses")]
    [Authorize]
    public async Task<IActionResult> GetSlaPauses(
        Guid taskId, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetTaskSlaPausesQuery(taskId), cancellationToken)));

    [HttpPost("{taskId:guid}/sla/pause")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> PauseSla(
        Guid taskId, CancellationToken cancellationToken)
    {
        await sender.Send(new PauseTaskSlaCommand(taskId), cancellationToken);
        return NoContent();
    }

    [HttpPost("{taskId:guid}/sla/resume")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> ResumeSla(
        Guid taskId, CancellationToken cancellationToken)
    {
        await sender.Send(new ResumeTaskSlaCommand(taskId), cancellationToken);
        return NoContent();
    }

    // F23-T004/P4c: evidências anexadas (§11) — upload multipart em disco
    // local (ADR-033); leitura = [Authorize], escrita = Operate. O nome
    // original é só metadado: em disco o arquivo é GUID + extensão.

    [HttpGet("{taskId:guid}/evidences")]
    [Authorize]
    public async Task<IActionResult> GetEvidence(
        Guid taskId, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetTaskEvidenceQuery(taskId), cancellationToken)));

    [HttpPost("{taskId:guid}/evidences")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> AddEvidence(
        Guid taskId,
        IFormFile? file,
        [FromForm] string? note,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            throw new ArgumentException("Nenhum arquivo recebido.", nameof(file));

        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);

        var result = await sender.Send(
            new UploadTaskEvidenceCommand(
                taskId, file.FileName, file.ContentType, buffer.ToArray(), note),
            cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpGet("evidences/{evidenceId:guid}/download")]
    [Authorize]
    public async Task<IActionResult> DownloadEvidence(
        Guid evidenceId, CancellationToken cancellationToken)
    {
        var download = await sender.Send(
            new GetTaskEvidenceDownloadQuery(evidenceId), cancellationToken);

        // Content-Disposition com o nome original (encoded); o corpo veio
        // da porta de storage (ADR-033).
        return File(download.Content, download.ContentType, download.FileName);
    }

    [HttpDelete("evidences/{evidenceId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> DeleteEvidence(
        Guid evidenceId, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteTaskEvidenceCommand(evidenceId), cancellationToken);
        return NoContent();
    }

    // F23-T004/P4d: fases/etapas dentro da task (§7) — leitura =
    // [Authorize], escrita = Operate; ordem por posição (gaps aceitos),
    // máx. 1 fase atual por task (o PUT limpa os irmãos).

    [HttpGet("{taskId:guid}/phases")]
    [Authorize]
    public async Task<IActionResult> GetPhases(
        Guid taskId, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetTaskPhasesQuery(taskId), cancellationToken)));

    [HttpPost("{taskId:guid}/phases")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> AddPhase(
        Guid taskId,
        [FromBody] AddTaskPhaseViewModel model,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new AddTaskPhaseCommand(taskId, model.Name), cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpPut("phases/{phaseId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> UpdatePhase(
        Guid phaseId,
        [FromBody] UpdateTaskPhaseViewModel model,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new UpdateTaskPhaseCommand(
                phaseId, model.Name, model.Current, model.Completed),
            cancellationToken)));

    [HttpDelete("phases/{phaseId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.Operate)]
    public async Task<IActionResult> DeletePhase(
        Guid phaseId, CancellationToken cancellationToken)
    {
        await sender.Send(new DeleteTaskPhaseCommand(phaseId), cancellationToken);
        return NoContent();
    }

    // F23-T004/P4d: timeline unificada tipada (§8) — leitura; tipos na
    // query string (types=estado,sla; desconhecido → 400 pt-BR) e limit
    // (clamp 1..500, default 100). Sem tabela nova: deriva de histórico
    // + comentários, com a ação `Commented` excluída (o comentário
    // entra pela fonte rica).

    [HttpGet("{taskId:guid}/timeline")]
    [Authorize]
    public async Task<IActionResult> GetTimeline(
        Guid taskId,
        [FromQuery] string? types,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetTaskTimelineQuery(taskId, types, limit ?? 100),
            cancellationToken)));
}
