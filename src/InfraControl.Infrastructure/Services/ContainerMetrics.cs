using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace InfraControl.Infrastructure.Services;

/// <summary>
/// Snapshot das métricas de container publicadas via OTLP. O cAdvisor não
/// enxerga containers quando o Docker usa containerd snapshotter (sem
/// GraphDriver/layerdb), então a própria API faz poll no Engine API e
/// publica gauges com label de serviço do Compose.
/// </summary>
public static class ContainerMetrics
{
    public const string MeterName = "InfraControl.Containers";

    public sealed record Sample(
        string Service,
        string Container,
        double? CpuPercent,
        double? MemoryPercent,
        long? MemoryBytes,
        long? NetworkRxBytes,
        long? NetworkTxBytes);

    private static readonly ConcurrentDictionary<string, Sample> Current = new();

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    static ContainerMetrics()
    {
        Meter.CreateObservableGauge("infracontrol_container_cpu_percent",
            () => Current.Values.Where(s => s.CpuPercent is not null)
                .Select(s => new Measurement<double>(s.CpuPercent!.Value,
                    new KeyValuePair<string, object?>("service", s.Service),
                    new KeyValuePair<string, object?>("container", s.Container))),
            unit: "%");

        Meter.CreateObservableGauge("infracontrol_container_memory_percent",
            () => Current.Values.Where(s => s.MemoryPercent is not null)
                .Select(s => new Measurement<double>(s.MemoryPercent!.Value,
                    new KeyValuePair<string, object?>("service", s.Service))),
            unit: "%");

        Meter.CreateObservableGauge("infracontrol_container_memory_bytes",
            () => Current.Values.Where(s => s.MemoryBytes is not null)
                .Select(s => new Measurement<long>(s.MemoryBytes!.Value,
                    new KeyValuePair<string, object?>("service", s.Service))),
            unit: "By");

        Meter.CreateObservableGauge("infracontrol_container_network_rx_bytes_total",
            () => Current.Values.Where(s => s.NetworkRxBytes is not null)
                .Select(s => new Measurement<long>(s.NetworkRxBytes!.Value,
                    new KeyValuePair<string, object?>("service", s.Service))),
            unit: "By");

        Meter.CreateObservableGauge("infracontrol_container_network_tx_bytes_total",
            () => Current.Values.Where(s => s.NetworkTxBytes is not null)
                .Select(s => new Measurement<long>(s.NetworkTxBytes!.Value,
                    new KeyValuePair<string, object?>("service", s.Service))),
            unit: "By");
    }

    /// <summary>Substitui o snapshot inteiro — containers que sumiram caem fora.</summary>
    public static void Publish(IReadOnlyList<Sample> samples)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in samples)
        {
            Current[s.Container] = s;
            seen.Add(s.Container);
        }

        foreach (var key in Current.Keys)
        {
            if (!seen.Contains(key))
                Current.TryRemove(key, out _);
        }
    }
}
