namespace InfraControl.Application.Reports.Modules.Ports;

using InfraControl.Application.Reports.Modules;

/// <summary>
/// F25-T004 — relatórios estruturados por módulo (docs/33 §9–§12), no molde
/// de <c>IReportIndicatorsRepository</c> (ADR-049). Composição por período,
/// sem armazenamento novo.
/// </summary>
public interface IModuleReportsRepository
{
    Task<TasksReportDto> GetTasksReportAsync(
        DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);

    Task<PdiReportDto> GetPdiReportAsync(
        DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);

    Task<KnowledgeReportDto> GetKnowledgeReportAsync(
        DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);

    Task<GerencialReportDto> GetGerencialReportAsync(
        DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
}
