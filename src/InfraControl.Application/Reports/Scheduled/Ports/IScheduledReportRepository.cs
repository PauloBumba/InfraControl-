using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Reports.Scheduled.Ports;

/// <summary>
/// Agenda de relatório por e-mail (singleton lógico por Kind).
/// </summary>
public interface IScheduledReportRepository : IRepository<ScheduledReport>
{
    /// <summary>Agenda do kind pedido (hoje só "General") ou null.</summary>
    Task<ScheduledReport?> GetByKindAsync(
        string kind, CancellationToken cancellationToken = default);

    /// <summary>
    /// Agendas ativas vencidas (NextRunAtUtc <= now) — varredura do worker.
    /// </summary>
    Task<IReadOnlyList<ScheduledReport>> ListDueAsync(
        DateTime nowUtc, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
