using System.Diagnostics;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Enums;

namespace InfraControl.Infrastructure.Monitoring;

/// <summary>
/// F30-T003b: check SnmpServerMemory — memória física via <c>hrStorageTable</c>
/// (HOST-RESOURCES-MIB RFC 2790, universal Windows/Linux). As linhas são
/// classificadas por <c>hrStorageType</c> (RAM=1, virtual/swap=2, discos=3+):
/// só as RAM entram no cálculo (bytes = unidades × allocationUnits). Swap
/// (type 2) é reportado informativo — a regra de alerta fica para a task de
/// prevenção (ficha 44 §H). Sem linha RAM utilizável, faz fallback para
/// <c>hrMemorySize</c> (total apenas — sem % o evaluator não avalia).
/// Opt-in — não é default do Server.
/// </summary>
public sealed class SnmpServerMemoryProvider(ISnmpClient snmpClient) : IMonitoringProvider
{
    /// <summary>hrStorageTable (colunas type/descr/alloc/size/used).</summary>
    public const string StorageTableOid = "1.3.6.1.2.1.25.2.3.1";

    /// <summary>hrMemorySize — RAM total em KBytes (fallback).</summary>
    public const string MemorySizeOid = "1.3.6.1.2.1.25.2.2";

    // hrStorageType (RFC 2790)
    private const string StorageTypeRam = "1";
    private const string StorageTypeVirtualMemory = "2";

    // colunas da hrStorageTable
    private const string ColType = "1";
    private const string ColDescr = "3";
    private const string ColAlloc = "4";
    private const string ColSize = "5";
    private const string ColUsed = "6";

    private const double BytesPerMb = 1024d * 1024d;

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    public string Name => CheckKind.SnmpServerMemory.ToString();

    public bool CanHandle(string providerKey)
        => string.Equals(providerKey, Name, StringComparison.OrdinalIgnoreCase);

    public async Task<MonitoringProviderResult> CheckAsync(
        string target,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(target))
            return new MonitoringProviderResult(
                false, TimeSpan.Zero, "Destino do SNMP é obrigatório.");

        var stopwatch = Stopwatch.StartNew();
        IReadOnlyList<SnmpVariable> rows;
        try
        {
            rows = await snmpClient.WalkAsync(
                target.Trim(), StorageTableOid, DefaultTimeout, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new MonitoringProviderResult(
                false, stopwatch.Elapsed, $"Falha ao consultar SNMP: {ex.Message}");
        }

        var entries = GroupByInstance(rows);
        var ram = entries.Values
            .Where(entry => entry.Type == StorageTypeRam)
            .Select(entry => ToBytes(entry))
            .Where(bytes => bytes is not null)
            .Select(bytes => bytes!)
            .ToArray();

        if (ram.Length == 0)
            return await FallbackToMemorySizeAsync(
                target.Trim(), stopwatch, cancellationToken);

        var totalBytes = ram.Sum(entry => entry.Total);
        var usedBytes = ram.Sum(entry => entry.Used);
        if (totalBytes <= 0)
            return await FallbackToMemorySizeAsync(
                target.Trim(), stopwatch, cancellationToken);

        var values = new Dictionary<string, string>
        {
            ["totalMB"] = ToMb(totalBytes),
            ["usedMB"] = ToMb(usedBytes),
            ["availableMB"] = ToMb(totalBytes - usedBytes),
            ["usedPercent"] = Percent(usedBytes, totalBytes)
        };

        var swap = entries.Values
            .Where(entry => entry.Type == StorageTypeVirtualMemory)
            .Select(entry => ToBytes(entry))
            .Where(bytes => bytes is not null)
            .Select(bytes => bytes!)
            .ToArray();
        var swapTotal = swap.Sum(entry => entry.Total);
        if (swapTotal > 0)
            values["swapPercent"] = Percent(swap.Sum(entry => entry.Used), swapTotal);

        stopwatch.Stop();
        return new MonitoringProviderResult(true, stopwatch.Elapsed, null, values);
    }

    /// <summary>
    /// Fallback: a tabela sem RAM ainda informa o total via hrMemorySize
    /// (KBytes) — informativo (sem usedPercent o evaluator não avalia).
    /// </summary>
    private async Task<MonitoringProviderResult> FallbackToMemorySizeAsync(
        string target,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        try
        {
            var size = await snmpClient.WalkAsync(
                target, MemorySizeOid, DefaultTimeout, cancellationToken);
            var kb = size
                .Select(variable => ParseLong(variable.Value))
                .FirstOrDefault(value => value.HasValue && value.Value > 0);
            if (kb.HasValue)
            {
                stopwatch.Stop();
                return new MonitoringProviderResult(
                    true, stopwatch.Elapsed, null,
                    new Dictionary<string, string>
                    {
                        ["totalMB"] = ToMb(kb.Value * 1024)
                    });
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // fallback indisponível — cai no falha abaixo.
        }

        stopwatch.Stop();
        return new MonitoringProviderResult(
            false, stopwatch.Elapsed,
            "Nenhuma memória física (hrStorageTable/hrMemorySize) encontrada no servidor.");
    }

    private sealed record StorageEntry(string? Type, string? Alloc, string? Size, string? Used);

    private sealed record StorageBytes(long Total, long Used);

    private static Dictionary<string, StorageEntry> GroupByInstance(
        IReadOnlyList<SnmpVariable> rows)
    {
        var byInstance = new Dictionary<string, StorageEntry>(StringComparer.Ordinal);
        foreach (var variable in rows)
        {
            var parts = variable.Oid.TrimEnd('.').Split('.');
            if (parts.Length < 3)
                continue;

            var column = parts[^2];
            var instance = parts[^1];
            if (!byInstance.TryGetValue(instance, out var entry))
                entry = new StorageEntry(null, null, null, null);

            entry = column switch
            {
                ColType => entry with { Type = variable.Value },
                ColAlloc => entry with { Alloc = variable.Value },
                ColSize => entry with { Size = variable.Value },
                ColUsed => entry with { Used = variable.Value },
                _ => entry
            };
            byInstance[instance] = entry;
        }

        return byInstance;
    }

    private static StorageBytes? ToBytes(StorageEntry entry)
    {
        var alloc = ParseLong(entry.Alloc);
        var size = ParseLong(entry.Size);
        var used = ParseLong(entry.Used);
        if (alloc is not > 0 || size is not > 0 || used is null || used.Value < 0)
            return null;

        return new StorageBytes(alloc.Value * size.Value, alloc.Value * used.Value);
    }

    private static long? ParseLong(string? raw)
        => long.TryParse(raw, out var parsed) && parsed >= 0 ? parsed : null;

    private static string ToMb(long bytes)
        => ((long)(bytes / BytesPerMb)).ToString();

    private static string Percent(long used, long total)
        => ((int)Math.Round(used * 100d / total, MidpointRounding.AwayFromZero))
            .ToString();
}
