using InfraControl.Application.Knowledge.Articles.Ports;
using InfraControl.Application.Pdi.Ports;
using InfraControl.Application.Tasks.History.Ports;
using InfraControl.Application.Tasks.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Knowledge.Articles.Commands;

// F24-T006/K6 (spec docs/32 §2–§4, ADR-035): "Gerar Knowledge" ao
// concluir Task/PDI — rascunho montado SOMENTE dos registros existentes
// (nunca inventa), sempre nasce Draft → revisão (ADR-034). Sem LLM: o
// repo não tem infra de IA (docs/31 §12) e a §3 exige "conteúdo baseado
// nos registros existentes" — montagem determinística atende literalmente.

public sealed record GenerateTaskKnowledgeCommand(
    Guid TaskId) : BaseCommand<KnowledgeArticleDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

public sealed class GenerateTaskKnowledgeCommandHandler(
    ITaskItemRepository taskRepository,
    ITaskHistoryRepository historyRepository,
    ITaskCriterionRepository criterionRepository,
    ITaskPhaseRepository phaseRepository,
    ITaskEvidenceRepository evidenceRepository,
    ITaskCommentRepository commentRepository,
    ITaskImpedimentRepository impedimentRepository,
    IKnowledgeArticleRepository articleRepository,
    IKnowledgeArticleVersionRepository versionRepository,
    IKnowledgeArticleOriginRepository originRepository,
    ICurrentUserContext currentUser,
    ILogger<GenerateTaskKnowledgeCommandHandler> logger)
    : IRequestHandler<GenerateTaskKnowledgeCommand, KnowledgeArticleDto>
{
    public async Task<KnowledgeArticleDto> Handle(
        GenerateTaskKnowledgeCommand request, CancellationToken cancellationToken)
    {
        var task = await taskRepository.GetByIdAsync(request.TaskId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Task '{request.TaskId}' não encontrada.");

        // docs/32 §2/§3: "Ao concluir uma Task" — só concluída gera
        // (Resolved = concluída, Closed = concluída e fechada).
        if (task.State is not (TaskState.Resolved or TaskState.Closed))
            throw new InvalidOperationException(
                "A task precisa estar concluída para gerar Knowledge.");

        var subtaskStats = await taskRepository.GetSubtaskStatsAsync(
            [task.Id], cancellationToken);
        var history = await historyRepository.GetByTaskIdAsync(
            task.Id, cancellationToken);
        var criteria = await criterionRepository.GetByTaskIdAsync(
            task.Id, cancellationToken);
        var phases = await phaseRepository.GetByTaskIdAsync(
            task.Id, cancellationToken);
        var evidences = await evidenceRepository.GetByTaskIdAsync(
            task.Id, cancellationToken);
        var comments = await commentRepository.GetByTaskIdAsync(
            task.Id, cancellationToken);
        var impediments = await impedimentRepository.GetByTaskIdAsync(
            task.Id, cancellationToken);

        var (title, content) = KnowledgeDraftComposer.ComposeFromTask(
            task,
            subtaskStats.GetValueOrDefault(task.Id, new TaskSubtaskStats(0, 0, 0)),
            history, criteria, phases, evidences, comments, impediments);

        var article = KnowledgeArticle.Create(
            title, content,
            categoryId: null, type: null, description: null,
            tags: null, responsibleId: null,
            authorId: currentUser.UserId);

        await articleRepository.AddAsync(article, cancellationToken);

        // K3: a criação é a v1.0 — motivo registra a proveniência (§8).
        await versionRepository.AddAsync(
            KnowledgeArticleVersion.SnapshotOf(
                article,
                $"Geração automática a partir da Task '{task.Id}'",
                currentUser.UserId),
            cancellationToken);

        // K4: "criada a partir de" — origem Task (§9/§10).
        await originRepository.AddAsync(
            KnowledgeArticleOrigin.Create(
                article.Id, KnowledgeOriginType.Task, task.Id),
            cancellationToken);

        await articleRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "KnowledgeArticle {ArticleId} generated from Task {TaskId} ({Title}).",
            article.Id, task.Id, article.Title);

        return await KnowledgeArticleDto.FromAsync(
            article, articleRepository, cancellationToken);
    }
}

public sealed record GeneratePdiKnowledgeCommand(
    Guid UserId) : BaseCommand<KnowledgeArticleDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;
}

/// <summary>
/// docs/32 §4 — "Ao concluir um PDI, o sistema poderá identificar
/// conteúdo potencialmente útil": rascunho a partir dos registros do
/// plano concluído (mesmas regras do ADR-035, origem Pdi no K4).
/// </summary>
public sealed class GeneratePdiKnowledgeCommandHandler(
    IPdiRepository pdiRepository,
    IPdiStructureRepository structureRepository,
    IKnowledgeArticleRepository articleRepository,
    IKnowledgeArticleVersionRepository versionRepository,
    IKnowledgeArticleOriginRepository originRepository,
    ICurrentUserContext currentUser,
    ILogger<GeneratePdiKnowledgeCommandHandler> logger)
    : IRequestHandler<GeneratePdiKnowledgeCommand, KnowledgeArticleDto>
{
    public async Task<KnowledgeArticleDto> Handle(
        GeneratePdiKnowledgeCommand request, CancellationToken cancellationToken)
    {
        // Mesma checagem de dono dos endpoints de PDI (Admin pode tudo).
        var isAdmin = currentUser.Roles.Contains(UserRole.Admin.ToString());
        if (!isAdmin && currentUser.UserId != request.UserId)
            throw new UnauthorizedAccessException(
                "Você só pode gerar Knowledge do seu próprio PDI.");

        var plan = await pdiRepository.GetByUserIdAsync(
            request.UserId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"PDI do usuário '{request.UserId}' não encontrado.");

        // docs/32 §4: "Ao concluir um PDI" — só concluído gera.
        if (plan.Status != PdiStatus.Completed)
            throw new InvalidOperationException(
                "O PDI precisa estar concluído para gerar Knowledge.");

        var rows = await structureRepository.GetTreeAsync(
            plan.UserId, cancellationToken);
        var competencies = await structureRepository.ListCompetenciesAsync(
            cancellationToken);
        var history = await pdiRepository.ListHistoryAsync(
            plan.UserId, cancellationToken);

        var (title, content) = KnowledgeDraftComposer.ComposeFromPdi(
            plan, rows.Objectives, competencies, history);

        var article = KnowledgeArticle.Create(
            title, content,
            categoryId: null, type: null, description: null,
            tags: null, responsibleId: null,
            authorId: currentUser.UserId);

        await articleRepository.AddAsync(article, cancellationToken);

        await versionRepository.AddAsync(
            KnowledgeArticleVersion.SnapshotOf(
                article,
                $"Geração automática a partir do PDI de '{plan.UserId}'",
                currentUser.UserId),
            cancellationToken);

        await originRepository.AddAsync(
            KnowledgeArticleOrigin.Create(
                article.Id, KnowledgeOriginType.Pdi, plan.Id),
            cancellationToken);

        await articleRepository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "KnowledgeArticle {ArticleId} generated from PDI {PdiUserId} ({Title}).",
            article.Id, plan.UserId, article.Title);

        return await KnowledgeArticleDto.FromAsync(
            article, articleRepository, cancellationToken);
    }
}
