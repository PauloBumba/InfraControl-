using InfraControl.Application.Monitoring.Checks.Ports;
using InfraControl.Application.Sla.Slos.Bindings.Ports;
using InfraControl.Domain.Entities;

namespace InfraControl.Application.Sla.Slos.Bindings;

/// <summary>
/// ADR-068: sincroniza os vínculos Slo ↔ MonitoringCheck do formulário.
/// Valida que todo id existe (400 pt-BR) e resolve o diff em memória —
/// adiciona os novos, remove os que saíram. NÃO persiste.
/// </summary>
public static class SloCheckBindingSync
{
    public static async Task SyncAsync(
        Guid sloId,
        IReadOnlyCollection<Guid>? requestedCheckIds,
        ISloCheckBindingRepository bindings,
        IMonitoringCheckRepository checks,
        CancellationToken cancellationToken)
    {
        var requested = (requestedCheckIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        if (requested.Length > 0)
        {
            var found = await checks.GetByIdsAsync(requested, cancellationToken);
            var foundIds = found.Select(check => check.Id).ToHashSet();
            var missing = requested.FirstOrDefault(id => !foundIds.Contains(id));
            if (missing != Guid.Empty)
                throw new ArgumentException(
                    $"Check de monitoramento '{missing}' não encontrado.");
        }

        var existing = await bindings.GetBySloIdAsync(sloId, cancellationToken);
        var existingIds = existing.Select(b => b.MonitoringCheckId).ToHashSet();
        var requestedSet = requested.ToHashSet();

        foreach (var binding in existing.Where(b => !requestedSet.Contains(b.MonitoringCheckId)))
            bindings.Remove(binding);

        foreach (var checkId in requested.Where(id => !existingIds.Contains(id)))
            await bindings.AddAsync(
                SloCheckBinding.Create(sloId, checkId), cancellationToken);
    }
}
