using InfraControl.Application.Monitoring.Results.Ports;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Sla.Slos.Bindings;

/// <summary>
/// ADR-068: cálculo puro da disponibilidade de um SLO — % de polls com
/// sucesso dos pares (DeviceId, Kind) das checks habilitadas vinculadas.
/// <paramref name="counts"/> já deve vir agregado desde o início da
/// janela do SLO. Retorna null quando não há nenhum poll ("sem dado"
/// nunca vira 100%).
/// </summary>
public static class SloAvailabilityCalculator
{
    public static decimal? ComputePercent(
        IEnumerable<(Guid DeviceId, CheckKind Kind)> boundPairs,
        IReadOnlyList<DeviceKindPollCount> counts)
    {
        var pairs = boundPairs.ToHashSet();
        if (pairs.Count == 0)
            return null;

        var total = 0;
        var succeeded = 0;
        foreach (var count in counts)
        {
            if (!pairs.Contains((count.DeviceId, count.Kind)))
                continue;
            total += count.Total;
            succeeded += count.Succeeded;
        }

        if (total == 0)
            return null;

        return Math.Round(succeeded * 100m / total, 3);
    }
}
