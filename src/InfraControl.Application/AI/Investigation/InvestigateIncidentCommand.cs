using System.Diagnostics;
using InfraControl.Application.AI.Chat;
using InfraControl.Application.AI.Context;
using InfraControl.Application.Incidents;
using InfraControl.Application.Incidents.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.AI.Investigation;

/// <summary>Resultado da investigação de um incidente (ADR-053 §2/§5).</summary>
public sealed record AiInvestigationDto(
    string Analysis,
    IReadOnlyList<AiChatEvidenceDto> Evidences,
    string Provider,
    string Model,
    bool UsedFallback);

/// <summary>
/// Investiga um incidente com IA (ADR-053 · Fase C): dados do incidente
/// + Context Engine (ADR-038) → capacidade <c>IncidentAnalysis</c>
/// (ADR-036/§15). Operate (ADR-030 §3): ação operacional; a análise é
/// LEVEL 0 — explica e apresenta hipóteses, nunca executa (ADR-040 §1).
/// </summary>
public sealed record InvestigateIncidentCommand(Guid IncidentId)
    : BaseCommand<AiInvestigationDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class InvestigateIncidentCommandHandler(
    IMediator mediator,
    IRetrievalService retrieval,
    IContextBuilder contextBuilder,
    IAiGateway gateway,
    ICurrentUserContext currentUser) : IRequestHandler<InvestigateIncidentCommand, AiInvestigationDto>
{
    private const int RetrievalLimit = 8;

    public async Task<AiInvestigationDto> Handle(
        InvestigateIncidentCommand request,
        CancellationToken cancellationToken)
    {
        // GetIncidentQuery (Read) devolve o incidente + alertas —
        // KeyNotFoundException vira 404 no GlobalExceptionHandler.
        var incident = await mediator.Send(
            new GetIncidentQuery(request.IncidentId), cancellationToken);

        var term = string.IsNullOrWhiteSpace(incident.Description)
            ? incident.Title
            : $"{incident.Title} {incident.Description}";
        var traceId = Activity.Current?.TraceId.ToString();

        var retrievalResult = await retrieval.RetrieveAsync(
            new RetrievalRequest(term, Limit: RetrievalLimit),
            cancellationToken);
        var evidenceSet = contextBuilder.Build(
            retrievalResult.Hits, traceId, tool: "incident-investigation");

        var completion = await gateway.CompleteAsync(
            new AiCompletionRequest(
                AiCapability.IncidentAnalysis,
                BuildPrompt(incident),
                BuildSystemPrompt(evidenceSet),
                TraceId: traceId,
                Actor: currentUser.UserName),
            cancellationToken);

        return new AiInvestigationDto(
            completion.Text,
            evidenceSet.Evidences
                .Select(evidence => new AiChatEvidenceDto(
                    evidence.Source.ToString(),
                    evidence.SourceType,
                    evidence.Citation,
                    evidence.Confidence.ToString(),
                    evidence.TimestampUtc))
                .ToList(),
            completion.ProviderName,
            completion.Model,
            completion.UsedFallback);
    }

    /// <summary>
    /// System prompt de investigação LEVEL 0 (ADR-053 §3 · ADR-040 §1):
    /// hipóteses com confiança, citação obrigatória, sem invenção e sem
    /// recomendação de ação de escrita.
    /// </summary>
    private static string BuildSystemPrompt(Context.EvidenceSet evidenceSet)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine(
            "Você é o investigador de incidentes de TI do InfraControl (IA).");
        builder.AppendLine(
            "Nível de autonomia: LEVEL 0 — somente leitura: analisar, correlacionar e "
            + "explicar; NÃO recomende ações de escrita nem alterações de configuração.");
        builder.AppendLine("Regras:");
        builder.AppendLine(
            "- Produza uma análise objetiva com hipóteses de causa classificadas por "
            + "confiança (Probable / Possible / Unknown) — nunca apresente hipótese como fato confirmado.");
        builder.AppendLine(
            "- Cite as Evidências entre colchetes, ex.: [E1]. Sem evidência, declare a "
            + "limitação — NUNCA invente IPs, eventos, logs ou procedimentos.");
        builder.AppendLine("- Responda em português do Brasil, direto e operacional.");

        if (evidenceSet.IsEmpty)
        {
            builder.AppendLine();
            builder.AppendLine(
                "Evidências de Knowledge: NENHUMA — baseie a análise apenas nos dados do "
                + "incidente abaixo e declare que não há contexto histórico recuperado.");
            return builder.ToString();
        }

        builder.AppendLine();
        builder.AppendLine("Evidências de Knowledge:");
        for (var index = 0; index < evidenceSet.Evidences.Count; index++)
        {
            var evidence = evidenceSet.Evidences[index];
            builder.AppendLine(
                $"[E{index + 1}] ({evidence.Source} · {evidence.Confidence} · "
                + $"{evidence.TimestampUtc:yyyy-MM-dd HH:mm}Z) {evidence.Citation} — {evidence.Data}");
        }

        return builder.ToString();
    }

    private static string BuildPrompt(IncidentDto incident)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("Incidente a investigar:");
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
        if (incident.ResolvedAt is DateTime resolvedAt)
        {
            builder.AppendLine($"- Resolvido em: {resolvedAt:yyyy-MM-dd HH:mm}Z");
        }
        builder.AppendLine($"- Alerts vinculados: {incident.AlertCount}");
        builder.AppendLine();
        builder.Append("Investigue o incidente acima.");
        return builder.ToString();
    }
}
