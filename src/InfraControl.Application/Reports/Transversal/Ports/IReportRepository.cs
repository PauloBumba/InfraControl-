namespace InfraControl.Application.Reports.Transversal.Ports;

/// <summary>
/// Read-model do relatório transversal (ADR-049). Uma única leitura
/// composta (GROUP BY por status nos 5 agregados + média de resolução de
/// incidente) em vez de repassar pelos queries de cada módulo (vários são
/// Administer e quebrariam um relatório Operate no meio). Implementação em
/// Infrastructure (<c>ReportRepository</c>), registro automático F0-T024.
/// </summary>
public interface IReportRepository
{
    Task<TransversalReportDto> GetSnapshotAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default);
}
