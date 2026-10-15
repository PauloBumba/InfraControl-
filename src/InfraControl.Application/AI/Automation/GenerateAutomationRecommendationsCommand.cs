using System.Diagnostics;
using System.Text.Json;
using InfraControl.Application.AI.Context;
using InfraControl.Application.Incidents;
using InfraControl.Application.Incidents.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.AI.Automation;

/// <summary>Uma recomendação para o humano decidir (LEVEL 1).</summary>
public sealed record AutomationRecommendationItem(
    string Title,
    string Rationale,
    string Priority);

/// <summary>Lista de recomendações geradas (Fase F · ADR-055).</summary>
public sealed record AutomationRecommendationsDto(
    IReadOnlyList<AutomationRecommendationItem> Items,
    string Provider,
    string Model,
    bool UsedFallback);

/// <summary>
/// Automation Agent em LEVEL 1 (ADR-055 · blueprint §38–§39): Consultar,
/// Analisar e RECOMENDAR — sem escrita, sem aprovação, sem execução.
/// Sinal determinístico (incidentes ativos) → capacidade
/// <c>Automation</c>; sem sinais devolve lista vazia sem chamar o LLM.
/// </summary>
public sealed record GenerateAutomationRecommendationsCommand
    : BaseCommand<AutomationRecommendationsDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class GenerateAutomationRecommendationsCommandHandler(
    IMediator mediator,
    IRetrievalService retrieval,
    IContextBuilder contextBuilder,
    IAiGateway gateway)
    : IRequestHandler<GenerateAutomationRecommendationsCommand, AutomationRecommendationsDto>
{
    private static readonly string[] ActiveStatuses =
        ["Open", "Investigating", "Identified", "Monitoring"];

    private const int StatusPageSize = 5;
    private const int MaxSignals = 8;
    private const int RetrievalLimit = 6;

    public async Task<AutomationRecommendationsDto> Handle(
        GenerateAutomationRecommendationsCommand request,
        CancellationToken cancellationToken)
    {
        var signals = await CollectSignalsAsync(cancellationToken);
        if (signals.Count == 0)
        {
            // Guardrail de honestidade (ADR-055 §2): sem sinal, sem
            // recomendação — e sem custo/alucinação de LLM.
            return new AutomationRecommendationsDto([], string.Empty, string.Empty, false);
        }

        var term = string.Join(" ", signals.Select(signal => signal.Title));
        var traceId = Activity.Current?.TraceId.ToString();

        var retrievalResult = await retrieval.RetrieveAsync(
            new RetrievalRequest(term, Limit: RetrievalLimit), cancellationToken);
        var evidenceSet = contextBuilder.Build(
            retrievalResult.Hits, traceId, tool: "automation-recommendations");

        var completion = await gateway.CompleteAsync(
            new AiCompletionRequest(
                AiCapability.Automation,
                BuildPrompt(signals),
                BuildSystemPrompt(evidenceSet),
                TraceId: traceId,
                Actor: null),
            cancellationToken);

        var items = AiAutomationRecommendationsJson.Parse(completion.Text);
        return new AutomationRecommendationsDto(
            items,
            completion.ProviderName,
            completion.Model,
            completion.UsedFallback);
    }

    private async Task<IReadOnlyList<ActiveIncidentSignal>> CollectSignalsAsync(
        CancellationToken cancellationToken)
    {
        var signals = new List<ActiveIncidentSignal>();
        foreach (var status in ActiveStatuses)
        {
            var page = await mediator.Send(
                new SearchIncidentsQuery(Status: status, Page: 1, PageSize: StatusPageSize),
                cancellationToken);
            signals.AddRange(page.Items.Select(incident => new ActiveIncidentSignal(
                incident.Title,
                incident.Severity,
                incident.Status,
                incident.OpenedAt)));
        }

        return signals
            .OrderByDescending(signal => signal.OpenedAt)
            .Take(MaxSignals)
            .ToList();
    }

    /// <summary>
    /// System prompt LEVEL 1 (ADR-055 §4 · blueprint §39/§40): recomendar
    /// citando os dados, nunca executar nem descrever comandos a rodar.
    /// </summary>
    private static string BuildSystemPrompt(Context.EvidenceSet evidenceSet)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine(
            "Você é o assistente de automação do InfraControl (IA).");
        builder.AppendLine(
            "Nível de autonomia: LEVEL 1 — Consultar, Analisar e RECOMENDAR (blueprint §39).");
        builder.AppendLine(
            "Você NÃO executa ações, NÃO solicita aprovação e NÃO descreve comandos "
            + "para rodar — decidir e executar é do humano (LEVEL 2+).");
        builder.AppendLine("Produza no máximo 5 recomendações; em cada uma:");
        builder.AppendLine(
            "- title: curto e acionável (≤ 120 caracteres);");
        builder.AppendLine(
            "- rationale: motivo citando OS DADOS dos sinais e do knowledge "
            + "(nunca invente), terminando com a verificação prévia que o humano "
            + "deve fazer antes de agir;");
        builder.AppendLine(
            "- priority: \"alta\" | \"média\" | \"baixa\".");
        builder.AppendLine("Regras:");
        builder.AppendLine(
            "- Baseie-se SOMENTE nos sinais e no knowledge fornecidos; sem base "
            + "suficiente, não recomende aquele item.");
        builder.AppendLine(
            "- Guardrails (blueprint §40): NUNCA recomende apagar dados, alterar "
            + "infraestrutura/rede/VLAN, executar comandos arbitrários ou alterar "
            + "usuários/permissões.");
        builder.AppendLine(
            "- Responda APENAS com JSON válido, sem cercas de markdown: "
            + "{\"items\": [{\"title\": \"...\", \"rationale\": \"...\", \"priority\": \"...\"}]}");

        if (evidenceSet.IsEmpty)
        {
            builder.AppendLine();
            builder.AppendLine(
                "Knowledge relacionado: NENHUM recuperado — recomende apenas a "
                + "verificação/observação dos próprios sinais.");
            return builder.ToString();
        }

        builder.AppendLine();
        builder.AppendLine("Knowledge relacionado (cite no rationale se usar):");
        for (var index = 0; index < evidenceSet.Evidences.Count; index++)
        {
            var evidence = evidenceSet.Evidences[index];
            builder.AppendLine(
                $"[E{index + 1}] ({evidence.Source} · {evidence.Confidence}) "
                + $"{evidence.Citation} — {evidence.Data}");
        }

        return builder.ToString();
    }

    private static string BuildPrompt(IReadOnlyList<ActiveIncidentSignal> signals)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("Sinais atuais — incidentes (mais recentes primeiro):");
        foreach (var signal in signals)
        {
            builder.AppendLine(
                $"- [{signal.Severity}/{signal.Status}] {signal.Title} "
                + $"(aberto em {signal.OpenedAt:yyyy-MM-dd HH:mm}Z)");
        }
        builder.AppendLine();
        builder.Append("Gere as recomendações para o humano analisar.");
        return builder.ToString();
    }

    private sealed record ActiveIncidentSignal(
        string Title, string Severity, string Status, DateTime OpenedAt);
}

/// <summary>
/// Parse tolerante do contrato JSON das recomendações (ADR-055 §3):
/// extrai o objeto da resposta, clampia títulos/justificativas e
/// normaliza a prioridade — resposta malformada vira 1 item com o texto.
/// </summary>
internal static class AiAutomationRecommendationsJson
{
    internal const int TitleMaxLength = 120;
    internal const int RationaleMaxLength = 2000;
    internal const int MaxItems = 5;

    private sealed record ItemJson(string? Title, string? Rationale, string? Priority);
    private sealed record EnvelopeJson(List<ItemJson>? Items);

    public static IReadOnlyList<AutomationRecommendationItem> Parse(string raw)
    {
        var text = raw ?? string.Empty;
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
        {
            try
            {
                var envelope = JsonSerializer.Deserialize<EnvelopeJson>(
                    text[start..(end + 1)],
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                var items = envelope?.Items?
                    .Where(item => !string.IsNullOrWhiteSpace(item.Title)
                        && !string.IsNullOrWhiteSpace(item.Rationale))
                    .Take(MaxItems)
                    .Select(item => new AutomationRecommendationItem(
                        Clamp(item.Title!.Trim(), TitleMaxLength),
                        Clamp(item.Rationale!.Trim(), RationaleMaxLength),
                        NormalizePriority(item.Priority)))
                    .ToList();
                if (items is { Count: > 0 })
                {
                    return items;
                }
            }
            catch (JsonException)
            {
                // Resposta malformada — fallback abaixo (ADR-055 §3).
            }
        }

        var fallbackText = string.IsNullOrWhiteSpace(text)
            ? "Não foi possível gerar recomendações estruturadas — tente novamente."
            : text.Trim();
        return
        [
            new AutomationRecommendationItem(
                "Recomendações (texto do modelo)",
                Clamp(fallbackText, RationaleMaxLength),
                "média"),
        ];
    }

    private static string NormalizePriority(string? priority)
        => priority?.Trim().ToLowerInvariant() switch
        {
            "alta" or "high" => "alta",
            "baixa" or "low" => "baixa",
            _ => "média",
        };

    private static string Clamp(string value, int max)
        => value.Length <= max ? value : value[..max];
}
