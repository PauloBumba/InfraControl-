using System.Diagnostics;
using System.Text.Json;
using InfraControl.Application.AI.Context;
using InfraControl.Application.Incidents;
using InfraControl.Application.Incidents.Queries;
using InfraControl.Application.Knowledge.Articles;
using InfraControl.Application.Knowledge.Articles.Ports;
using InfraControl.Application.Tasks.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.AI.Knowledge;

/// <summary>
/// Rascunho de artigo redigido pela IA (ADR-054 · Fase D). O artigo
/// nasce <c>Draft</c> por domínio (workflow ADR-034 intacto) com origem
/// K4 declarando a fonte — publicar é transição humana.
/// </summary>
public sealed record GenerateIncidentKnowledgeDraftCommand(Guid IncidentId)
    : BaseCommand<KnowledgeArticleDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed record GenerateTaskKnowledgeDraftCommand(Guid TaskId)
    : BaseCommand<KnowledgeArticleDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class GenerateIncidentKnowledgeDraftCommandHandler(
    IMediator mediator,
    IRetrievalService retrieval,
    IContextBuilder contextBuilder,
    IAiGateway gateway,
    IKnowledgeArticleRepository articleRepository,
    IKnowledgeArticleVersionRepository versionRepository,
    IKnowledgeArticleOriginRepository originRepository,
    ICurrentUserContext currentUser,
    ILogger<GenerateIncidentKnowledgeDraftCommandHandler> logger)
    : IRequestHandler<GenerateIncidentKnowledgeDraftCommand, KnowledgeArticleDto>
{
    public async Task<KnowledgeArticleDto> Handle(
        GenerateIncidentKnowledgeDraftCommand request,
        CancellationToken cancellationToken)
    {
        // GetIncidentQuery (Read) — KeyNotFoundException vira 404.
        var incident = await mediator.Send(
            new GetIncidentQuery(request.IncidentId), cancellationToken);

        var (title, content) = await AiKnowledgeDraftRetrieval.DraftAsync(
            retrieval, contextBuilder, gateway,
            term: string.IsNullOrWhiteSpace(incident.Description)
                ? incident.Title
                : $"{incident.Title} {incident.Description}",
            BuildPrompt(incident),
            fallbackTitle: incident.Title,
            cancellationToken);

        var article = await AiKnowledgeDraftPersist.SaveAsync(
            articleRepository, versionRepository, originRepository, currentUser,
            title, content,
            versionReason: $"Rascunho gerado com IA a partir do Incidente '{incident.Title}' (revisão humana obrigatória)",
            originType: KnowledgeOriginType.Incident,
            originId: incident.Id,
            cancellationToken);

        logger.LogInformation(
            "KnowledgeArticle {ArticleId} AI-drafted from Incident {IncidentId} ({Title}).",
            article.Id, incident.Id, article.Title);

        return await KnowledgeArticleDto.FromAsync(
            article, articleRepository, cancellationToken);
    }

    internal static string BuildPrompt(IncidentDto incident)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("Fonte — Incidente:");
        builder.AppendLine($"- Título: {incident.Title}");
        builder.AppendLine($"- Severidade: {incident.Severity}");
        builder.AppendLine($"- Status: {incident.Status}");
        builder.AppendLine($"- Aberto em: {incident.OpenedAt:yyyy-MM-dd HH:mm}Z");
        if (incident.DeviceId is Guid deviceId)
        {
            builder.AppendLine($"- Device: {deviceId}");
        }
        if (!string.IsNullOrWhiteSpace(incident.Description))
        {
            builder.AppendLine($"- Descrição: {incident.Description}");
        }
        if (!string.IsNullOrWhiteSpace(incident.RootCause))
        {
            builder.AppendLine($"- Causa raiz registrada: {incident.RootCause}");
        }
        builder.AppendLine($"- Alerts vinculados: {incident.AlertCount}");
        builder.AppendLine();
        builder.Append("Redija o rascunho do artigo de knowledge a partir deste incidente.");
        return builder.ToString();
    }
}

public sealed class GenerateTaskKnowledgeDraftCommandHandler(
    ITaskItemRepository taskRepository,
    IRetrievalService retrieval,
    IContextBuilder contextBuilder,
    IAiGateway gateway,
    IKnowledgeArticleRepository articleRepository,
    IKnowledgeArticleVersionRepository versionRepository,
    IKnowledgeArticleOriginRepository originRepository,
    ICurrentUserContext currentUser,
    ILogger<GenerateTaskKnowledgeDraftCommandHandler> logger)
    : IRequestHandler<GenerateTaskKnowledgeDraftCommand, KnowledgeArticleDto>
{
    public async Task<KnowledgeArticleDto> Handle(
        GenerateTaskKnowledgeDraftCommand request,
        CancellationToken cancellationToken)
    {
        var task = await taskRepository.GetByIdAsync(request.TaskId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Task '{request.TaskId}' não encontrada.");

        // Mesmo gate documental da geração determinística (docs/32 §2).
        if (task.State is not (TaskState.Resolved or TaskState.Closed))
            throw new InvalidOperationException(
                "A task precisa estar concluída para gerar Knowledge.");

        var (title, content) = await AiKnowledgeDraftRetrieval.DraftAsync(
            retrieval, contextBuilder, gateway,
            term: string.IsNullOrWhiteSpace(task.Description)
                ? task.Title
                : $"{task.Title} {task.Description}",
            BuildPrompt(task),
            fallbackTitle: task.Title,
            cancellationToken);

        var article = await AiKnowledgeDraftPersist.SaveAsync(
            articleRepository, versionRepository, originRepository, currentUser,
            title, content,
            versionReason: $"Rascunho gerado com IA a partir da Task '{task.Title}' (revisão humana obrigatória)",
            originType: KnowledgeOriginType.Task,
            originId: task.Id,
            cancellationToken);

        logger.LogInformation(
            "KnowledgeArticle {ArticleId} AI-drafted from Task {TaskId} ({Title}).",
            article.Id, task.Id, article.Title);

        return await KnowledgeArticleDto.FromAsync(
            article, articleRepository, cancellationToken);
    }

    internal static string BuildPrompt(TaskItem task)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("Fonte — Task:");
        builder.AppendLine($"- Título: {task.Title}");
        builder.AppendLine($"- Estado: {task.State}");
        builder.AppendLine($"- Prioridade: {task.Priority}");
        if (!string.IsNullOrWhiteSpace(task.Description))
        {
            builder.AppendLine($"- Descrição: {task.Description}");
        }
        if (!string.IsNullOrWhiteSpace(task.Result))
        {
            builder.AppendLine($"- Resultado registrado: {task.Result}");
        }
        builder.AppendLine();
        builder.Append("Redija o rascunho do artigo de knowledge a partir desta task concluída.");
        return builder.ToString();
    }
}

/// <summary>
/// Recupera contexto (ADR-038) e chama o gateway na capacidade
/// <c>KnowledgeGeneration</c> (ADR-037 §5 · docs/34 §31), parseando o
/// JSON estrito do modelo com fallback seguro (ADR-054 §5).
/// </summary>
internal static class AiKnowledgeDraftRetrieval
{
    private const int RetrievalLimit = 6;

    public static async Task<(string Title, string Content)> DraftAsync(
        IRetrievalService retrieval,
        IContextBuilder contextBuilder,
        IAiGateway gateway,
        string term,
        string prompt,
        string fallbackTitle,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.TraceId.ToString();
        var retrievalResult = await retrieval.RetrieveAsync(
            new RetrievalRequest(term, Limit: RetrievalLimit), cancellationToken);
        var evidenceSet = contextBuilder.Build(
            retrievalResult.Hits, traceId, tool: "knowledge-draft");

        var completion = await gateway.CompleteAsync(
            new AiCompletionRequest(
                AiCapability.KnowledgeGeneration,
                prompt,
                BuildSystemPrompt(evidenceSet),
                TraceId: traceId,
                Actor: null),
            cancellationToken);

        return AiKnowledgeDraftJson.Parse(completion.Text, fallbackTitle);
    }

    private static string BuildSystemPrompt(Context.EvidenceSet evidenceSet)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine(
            "Você é o engenheiro de knowledge do InfraControl (IA).");
        builder.AppendLine(
            "Nível de autonomia: LEVEL 0 — você redige um RASCUNHO para revisão "
            + "humana; não execute nem altere nada.");
        builder.AppendLine("Tarefa: redija um artigo de knowledge base em Markdown (pt-BR) "
            + "a partir SOMENTE dos dados fornecidos.");
        builder.AppendLine("Regras:");
        builder.AppendLine(
            "- NUNCA invente IPs, comandos, telas, eventos ou procedimentos não citados; "
            + "sem evidência, declare no corpo o que falta verificar.");
        builder.AppendLine(
            "- Estrutura do corpo: ## Contexto; ## Ocorrência; "
            + "## Verificações (somente leitura); ## Atenção (o que confirmar antes de agir).");
        builder.AppendLine("- Título: ≤ 200 caracteres, específico e objetivo.");
        builder.AppendLine("- Corpo: ≤ 20000 caracteres.");
        builder.AppendLine(
            "- Responda APENAS com JSON válido, sem cercas de markdown: "
            + "{\"title\": \"...\", \"content\": \"...\"}");

        if (evidenceSet.IsEmpty)
        {
            builder.AppendLine();
            builder.AppendLine(
                "Knowledge relacionado: NENHUM recuperado — baseie o rascunho apenas "
                + "nos dados da fonte e declare a ausência de procedimento histórico.");
            return builder.ToString();
        }

        builder.AppendLine();
        builder.AppendLine("Knowledge relacionado (cite como [E1], [E2]… no corpo se usar):");
        for (var index = 0; index < evidenceSet.Evidences.Count; index++)
        {
            var evidence = evidenceSet.Evidences[index];
            builder.AppendLine(
                $"[E{index + 1}] ({evidence.Source} · {evidence.Confidence}) "
                + $"{evidence.Citation} — {evidence.Data}");
        }

        return builder.ToString();
    }
}

/// <summary>
/// Parse tolerante do contrato JSON do rascunho (ADR-054 §5): extrai o
/// primeiro objeto JSON da resposta e clampia nos limites do domínio —
/// resposta malformada cai no fallback (título-fonte + texto bruto).
/// </summary>
internal static class AiKnowledgeDraftJson
{
    internal const int TitleMaxLength = 200;
    internal const int ContentMaxLength = 20_000;

    private sealed record DraftJson(string? Title, string? Content);

    public static (string Title, string Content) Parse(
        string raw, string fallbackTitle)
    {
        var text = raw ?? string.Empty;
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            try
            {
                var draft = JsonSerializer.Deserialize<DraftJson>(
                    text[start..(end + 1)],
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                var title = draft?.Title?.Trim();
                var content = draft?.Content?.Trim();
                if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(content))
                {
                    return (Clamp(title, TitleMaxLength), Clamp(content, ContentMaxLength));
                }
            }
            catch (JsonException)
            {
                // Resposta malformada — fallback abaixo (ADR-054 §5).
            }
        }

        var fallback = string.IsNullOrWhiteSpace(fallbackTitle)
            ? "Rascunho de knowledge"
            : fallbackTitle.Trim();
        var body = string.IsNullOrWhiteSpace(text)
            ? "_Conteúdo não gerado — refaça a geração._"
            : text.Trim();
        return (Clamp(fallback, TitleMaxLength), Clamp(body, ContentMaxLength));
    }

    private static string Clamp(string value, int max)
        => value.Length <= max ? value : value[..max];
}

/// <summary>
/// Persistência comum dos dois rascunhos: nasce <c>Draft</c> por domínio,
/// versão v1.0 (K3) e origem K4 — espelho da geração determinística
/// (ADR-035), com autoria IA declarada no motivo da versão.
/// </summary>
internal static class AiKnowledgeDraftPersist
{
    public static async Task<KnowledgeArticle> SaveAsync(
        IKnowledgeArticleRepository articleRepository,
        IKnowledgeArticleVersionRepository versionRepository,
        IKnowledgeArticleOriginRepository originRepository,
        ICurrentUserContext currentUser,
        string title,
        string content,
        string versionReason,
        KnowledgeOriginType originType,
        Guid originId,
        CancellationToken cancellationToken)
    {
        var article = KnowledgeArticle.Create(
            title, content,
            categoryId: null, type: null, description: null,
            tags: null, responsibleId: null,
            authorId: currentUser.UserId);

        await articleRepository.AddAsync(article, cancellationToken);
        await versionRepository.AddAsync(
            KnowledgeArticleVersion.SnapshotOf(article, versionReason, currentUser.UserId),
            cancellationToken);
        await originRepository.AddAsync(
            KnowledgeArticleOrigin.Create(article.Id, originType, originId),
            cancellationToken);
        await articleRepository.SaveChangesAsync(cancellationToken);
        return article;
    }
}
