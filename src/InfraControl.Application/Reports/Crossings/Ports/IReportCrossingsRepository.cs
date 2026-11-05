namespace InfraControl.Application.Reports.Crossings.Ports;

using InfraControl.Application.Reports.Crossings;

/// <summary>
/// F25-T002 — leitura dos cruzamentos integrados (docs/33 §5), no molde de
/// <c>IReportIndicatorsRepository</c> (ADR-049). Lê os vínculos N:N já
/// prontos (<c>task_incidents</c>, <c>task_knowledge_articles</c>,
/// <c>knowledge_article_origins</c>, <c>knowledge_article_incidents</c>,
/// <c>task_pdis</c>) sem expor os módulos internamente.
/// </summary>
public interface IReportCrossingsRepository
{
    Task<ReportCrossingsDto> GetCrossingsAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default);
}
