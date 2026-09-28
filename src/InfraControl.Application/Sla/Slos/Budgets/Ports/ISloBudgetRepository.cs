using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Domain;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Sla.Slos.Budgets.Ports;

public interface ISloBudgetRepository : IRepository<SloBudget>
{
    Task<PagedResult<SloBudget>> SearchBySloIdAsync(
        Guid sloId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Usado pelo DeleteSlo para bloquear exclusão com orçamentos (F8-T007 → 409).</summary>
    Task<bool> HasSloBudgetsAsync(Guid sloId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
