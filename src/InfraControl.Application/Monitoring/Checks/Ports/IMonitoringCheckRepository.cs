using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Monitoring.Checks.Ports;

public interface IMonitoringCheckRepository : IRepository<MonitoringCheck>
{
    /// <summary>Checks de um device, ordenados por <see cref="CheckKind"/>.</summary>
    Task<IReadOnlyList<MonitoringCheck>> GetByDeviceAsync(
        Guid deviceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sincroniza as linhas com a lista do device (F5-T001): adiciona as que
    /// faltam (Enabled=true), remove as fora da lista e preserva as
    /// existentes. NÃO persiste — quem chama grava no mesmo SaveChanges.
    /// </summary>
    Task SynchronizeFromProfileAsync(
        Guid deviceId,
        IEnumerable<CheckKind> checks,
        CancellationToken cancellationToken = default);
}
