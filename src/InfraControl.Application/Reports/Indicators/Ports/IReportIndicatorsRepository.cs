namespace InfraControl.Application.Reports.Indicators.Ports;

using InfraControl.Application.Reports.Indicators;

/// <summary>
/// F25-T001 — leitura composta dos indicadores detalhados (§2 Tasks,
/// §3 PDI, §4 Knowledge) numa única passada, no molde de
/// <c>IReportRepository</c> (ADR-049). Evita repassar pelos queries de
/// cada módulo (vários são Administer/operam por vertical).
/// </summary>
public interface IReportIndicatorsRepository
{
    Task<ReportIndicatorsDto> GetIndicatorsAsync(
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken = default);
}
