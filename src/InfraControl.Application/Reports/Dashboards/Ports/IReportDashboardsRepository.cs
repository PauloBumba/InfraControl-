namespace InfraControl.Application.Reports.Dashboards.Ports;

using InfraControl.Application.Reports.Dashboards;

/// <summary>
/// F25-T003 — leitura dos dashboards por papel (docs/33 §6–§8), no molde de
/// <c>IReportIndicatorsRepository</c> (ADR-049). Escopos: colaborador = usuário
/// atual; gestor = visão operacional ampla (domínio sem subordinação);
/// administrativo = consolidado com filtros.
/// </summary>
public interface IReportDashboardsRepository
{
    Task<ColaboradorDashboardDto> GetColaboradorAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<GestorDashboardDto> GetGestorAsync(
        CancellationToken cancellationToken = default);

    Task<AdministrativoDashboardDto> GetAdministrativoAsync(
        DateOnly? from,
        DateOnly? to,
        Guid? responsibleId,
        TaskKindFilter? kind,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Filtro de tipo de task para o dashboard administrativo (§8 "tipo").
/// </summary>
public enum TaskKindFilter
{
    Manual,
    Incident,
    Alert,
    Monitoring,
    Discovery,
    Maintenance,
    Change,
    Preventive,
    Corrective,
}
