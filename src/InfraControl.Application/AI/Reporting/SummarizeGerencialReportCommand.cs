using System.Text.Json;
using InfraControl.Application.Reports.Modules.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.AI.Reporting;

/// <summary>Interpretação do relatório gerencial (§16 — IA nos relatórios).</summary>
public sealed record AiReportSummaryDto(
    string Summary,
    string Provider,
    string Model,
    bool UsedFallback);

/// <summary>
/// F25-T006 §16 — interpretação por IA do relatório gerencial do período
/// (docs/33 §12 + §16): o read-model é a evidência; a IA INTERPRETA os
/// números — LEVEL 0 (ADR-040 §1), sem recomendação de ação e sem inventar
/// resultados. Mesma policy Operate do relatório-base.
/// </summary>
public sealed record SummarizeGerencialReportCommand(DateOnly? From, DateOnly? To)
    : BaseCommand<AiReportSummaryDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class SummarizeGerencialReportCommandHandler(
    IMediator mediator,
    IAiGateway gateway,
    ICurrentUserContext currentUser)
    : IRequestHandler<SummarizeGerencialReportCommand, AiReportSummaryDto>
{
    public async Task<AiReportSummaryDto> Handle(
        SummarizeGerencialReportCommand request,
        CancellationToken cancellationToken)
    {
        var report = await mediator.Send(
            new GetGerencialReportQuery(request.From, request.To),
            cancellationToken);

        var completion = await gateway.CompleteAsync(
            new AiCompletionRequest(
                AiCapability.Reporting,
                BuildPrompt(report),
                BuildSystemPrompt(),
                TraceId: System.Diagnostics.Activity.Current?.TraceId.ToString(),
                Actor: currentUser.UserName),
            cancellationToken);

        return new AiReportSummaryDto(
            completion.Text,
            completion.ProviderName,
            completion.Model,
            completion.UsedFallback);
    }

    /// <summary>
    /// System prompt LEVEL 0 (§16 · ADR-040 §1): interpretar SOMENTE os
    /// números do relatório gerencial, citando as contagens — sem causas
    /// inventadas e sem ação executável.
    /// </summary>
    private static string BuildSystemPrompt()
        => """
        Você é o analista de operações do InfraControl (IA).
        Nível de autonomia: LEVEL 0 — interpretar e explicar indicadores;
        NÃO recomende nem execute ações, comandos ou alterações de configuração.
        Tarefa: interprete o relatório gerencial (JSON) em português do Brasil, em Markdown curto:
        ## Leitura geral (2–3 frases)
        ## Destaques (3–5 bullets, sempre citando os números)
        ## Pontos de atenção (2–4 bullets — apenas o que os indicadores mostram)
        Regras:
        - Use SOMENTE os números do JSON; cite as contagens explicitamente.
        - Não invente causas nem eventos: correlacione apenas o que os indicadores exibem.
        - Se um indicador vier zerado ou ausente, diga isso — não suponha.
        - Máximo de ~200 palavras.
        """;

    private static string BuildPrompt(
        Reports.Modules.GerencialReportDto report)
    {
        var period = report.From is null && report.To is null
            ? "sem filtro (todo o histórico)"
            : $"de {report.From?.ToString("yyyy-MM-dd") ?? "início"} até {report.To?.ToString("yyyy-MM-dd") ?? "hoje"}";
        var json = JsonSerializer.Serialize(
            report, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return $"Período: {period}\nRelatório gerencial (JSON): {json}";
    }
}
