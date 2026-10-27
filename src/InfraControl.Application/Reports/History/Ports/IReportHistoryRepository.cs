namespace InfraControl.Application.Reports.History.Ports;

using InfraControl.Application.Reports.History;

/// <summary>
/// F25-T005 — leitura da série histórica de indicadores (docs/33 §13), no
/// molde de <c>IReportIndicatorsRepository</c> (ADR-049). Sem armazenamento
/// novo: agregação por mês no banco.
/// </summary>
public interface IReportHistoryRepository
{
    Task<ReportHistoryDto> GetHistoryAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default);
}
