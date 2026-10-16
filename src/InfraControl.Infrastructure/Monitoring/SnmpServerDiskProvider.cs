using System.Diagnostics;
using System.Text.Json;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Enums;

namespace InfraControl.Infrastructure.Monitoring;

/// <summary>
/// F30-T003c: check SnmpServerDisk — partições via <c>hrStorageTable</c>
/// (HOST-RESOURCES-MIB RFC 2790, universal Windows/Linux). As linhas são
/// classificadas por <c>hrStorageType</c>: fixed(3)/removable(4)/flash(8)
/// contam como disco; RAM(1)/swap(2)/rede(9)/desconhecidas são descartadas
/// (a memória é o check SnmpServerMemory). O pior disco (<c>worstPercent</c>)
/// é o alertável; a lista completa vai em <c>Values["disks"]</c> (JSON) para
/// a UI renderizar barra por partição. Opt-in — não é default do Server.
/// </summary>
public sealed class SnmpServerDiskProvider(ISnmpClient snmpClient) : IMonitoringProvider
{
    /// <summary>hrStorageTable (colunas type/descr/alloc/size/used).</summary>
    public const string StorageTableOid = "1.3.6.1.2.1.25.2.3.1";

    // hrStorageType (RFC 2790): só fixed/removable/flash são partições locais.
    private static readonly HashSet<string> DiskStorageTypes = ["3", "4", "8"];

    // colunas da hrStorageTable
    private const string ColType = "1";
    private const string ColDescr = "3";
    private const string ColAlloc = "4";
    private const string ColSize = "5";
    private const string ColUsed = "6";

    private const double BytesPerMb = 1024d * 1024d;

    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    public string Name => CheckKind.SnmpServerDisk.ToString();

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

        var disks = GroupByInstance(rows)
            .Where(pair => DiskStorageTypes.Contains(pair.Value.Type ?? string.Empty))
            .Select(pair => ToDisk(pair.Key, pair.Value))
            .Where(disk => disk is not null)
            .Select(disk => disk!)
            .OrderBy(disk => disk.Name, StringComparer.Ordinal)
            .ToArray();

        stopwatch.Stop();

        if (disks.Length == 0)
            return new MonitoringProviderResult(
                false, stopwatch.Elapsed,
                "Nenhuma partição de disco válida encontrada no servidor.");

        var worst = disks.MaxBy(disk => disk.UsedPercent)!;
        var values = new Dictionary<string, string>
        {
            ["diskCount"] = disks.Length.ToString(),
            ["worstDisk"] = worst.Name,
            ["worstPercent"] = worst.UsedPercent.ToString(),
            ["disks"] = JsonSerializer.Serialize(disks.Select(disk => new
            {
                name = disk.Name,
                usedPercent = disk.UsedPercent,
                totalMB = disk.TotalMb,
                usedMB = disk.UsedMb
            }))
        };

        return new MonitoringProviderResult(true, stopwatch.Elapsed, null, values);
    }

    private sealed record DiskInfo(string Name, int UsedPercent, long TotalMb, long UsedMb);

    private sealed record StorageEntry(string? Type, string? Descr, string? Alloc,
        string? Size, string? Used);

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
                entry = new StorageEntry(null, null, null, null, null);

            entry = column switch
            {
                ColType => entry with { Type = variable.Value },
                ColDescr => entry with { Descr = variable.Value },
                ColAlloc => entry with { Alloc = variable.Value },
                ColSize => entry with { Size = variable.Value },
                ColUsed => entry with { Used = variable.Value },
                _ => entry
            };
            byInstance[instance] = entry;
        }

        return byInstance;
    }

    private static DiskInfo? ToDisk(string instance, StorageEntry entry)
    {
        var alloc = ParseLong(entry.Alloc);
        var size = ParseLong(entry.Size);
        var used = ParseLong(entry.Used);
        if (alloc is not > 0 || size is not > 0 || used is null || used.Value < 0)
            return null;

        var totalBytes = alloc.Value * size.Value;
        var usedBytes = alloc.Value * used.Value;
        var name = string.IsNullOrWhiteSpace(entry.Descr) ? $"disk-{instance}" : entry.Descr;

        return new DiskInfo(
            name,
            (int)Math.Round(usedBytes * 100d / totalBytes, MidpointRounding.AwayFromZero),
            (long)(totalBytes / BytesPerMb),
            (long)(usedBytes / BytesPerMb));
    }

    private static long? ParseLong(string? raw)
        => long.TryParse(raw, out var parsed) && parsed >= 0 ? parsed : null;
}
