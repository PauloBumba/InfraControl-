using System.Text.Json;
using InfraControl.Application.Reports.Transversal;
using InfraControl.Application.Reports.Transversal.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.AI.Reporting;

/// <summary>Resumo interpretativo do relatório transversal (ADR-049 + IA).</summary>
public sealed record TransversalAiSummaryDto(
    string Summary,
    string Provider,
    string Model,
    bool UsedFallback);

/// <summary>
/// Resumo do relatório transversal com IA (Fase E · docs/34 §37 —
/// Reporting): o read-model (ADR-049) é a evidência; a IA INTERPRETA os
/// números — LEVEL 0 (ADR-040 §1), sem recomendação de ação. Mesma
/// policy Operate do relatório-base.
/// </summary>
public sealed record SummarizeTransversalReportCommand(DateOnly? From, DateOnly? To)
    : BaseCommand<TransversalAiSummaryDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class SummarizeTransversalReportCommandHandler(
    IMediator mediator,
    IAiGateway gateway,
    ICurrentUserContext currentUser)
    : IRequestHandler<SummarizeTransversalReportCommand, TransversalAiSummaryDto>
{
    public async Task<TransversalAiSummaryDto> Handle(
        SummarizeTransversalReportCommand request,
        CancellationToken cancellationToken)
    {
        // Mesmo read-model do GET /reports/transversal (Operate na query).
        var report = await mediator.Send(
            new GetTransversalReportQuery(request.From, request.To),
            cancellationToken);

        var completion = await gateway.CompleteAsync(
            new AiCompletionRequest(
                AiCapability.Reporting,
                BuildPrompt(report),
                BuildSystemPrompt(),
                TraceId: System.Diagnostics.Activity.Current?.TraceId.ToString(),
                Actor: currentUser.UserName),
            cancellationToken);

        return new TransversalAiSummaryDto(
            completion.Text,
            completion.ProviderName,
            completion.Model,
            completion.UsedFallback);
    }

    /// <summary>
    /// System prompt LEVEL 0 (§37 · ADR-040 §1): interpretar SOMENTE os
    /// números fornecidos, citando as contagens — sem causas inventadas
    /// e sem ação executável.
    /// </summary>
    private static string BuildSystemPrompt()
        => """
        Você é o analista de operações do InfraControl (IA).
        Nível de autonomia: LEVEL 0 — interpretar e explicar indicadores;
        NÃO recomende nem execute ações, comandos ou alterações de configuração.
        Tarefa: resuma o snapshot transversal (JSON) em português do Brasil, em Markdown curto:
        ## Leitura geral (2–3 frases)
        ## Destaques (3–5 bullets, sempre citando os números)
        ## Pontos de atenção (2–4 bullets — apenas o que os indicadores mostram)
        Regras:
        - Use SOMENTE os números do JSON; cite as contagens explicitamente.
        - Não invente causas nem eventos: correlacione apenas o que os indicadores exibem.
        - Se um indicador vier zerado ou ausente, diga isso — não suponha.
        - Máximo de ~200 palavras.
        """;

    private static string BuildPrompt(TransversalReportDto report)
    {
        var period = report.From is null && report.To is null
            ? "sem filtro (todo o histórico)"
            : $"de {report.From?.ToString("yyyy-MM-dd") ?? "início"} até {report.To?.ToString("yyyy-MM-dd") ?? "hoje"}";
        var json = JsonSerializer.Serialize(
            report, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return $"Período: {period}\nSnapshot (JSON): {json}";
    }
}
