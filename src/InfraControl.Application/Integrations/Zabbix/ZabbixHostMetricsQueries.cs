using InfraControl.Application.Integrations.Zabbix.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Integrations.Zabbix;

// F54-T001: leitura de itens e série temporal de um host Zabbix para os
// gráficos do dashboard (tráfego/CPU/memória/disponibilidade). Somente
// LEITURA; o client resolve autenticação/sessão. O history.get exige a
// tabela `history` (0=float, 3=unsigned) — consultamos as duas e
// deduplicamos por (item, clock), mesmo padrão do GetSwitchZabbixPortStatusQuery.

/// <summary>Item de um host Zabbix com metadados para o seletor de métricas.</summary>
public sealed record ZabbixHostItemDto(
    string ItemId,
    string Name,
    string Key,
    string Unit,
    int Type,
    bool Enabled,
    bool Unsupported,
    string? LastValue,
    DateTime? LastClockUtc);

/// <summary>Itens de um host (item.get) + último valor de cada um.</summary>
public sealed record GetZabbixHostItemsQuery(string HostId)
    : BaseQuery<IReadOnlyList<ZabbixHostItemDto>>, IAuthorizedRequest
{
    public override string CacheKey => $"{GetType().Name}:{HostId}";

    public override TimeSpan? CacheDuration => TimeSpan.FromSeconds(60);

    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Read;
}

public sealed class GetZabbixHostItemsQueryHandler(IZabbixClient client)
    : IRequestHandler<GetZabbixHostItemsQuery, IReadOnlyList<ZabbixHostItemDto>>
{
    public async Task<IReadOnlyList<ZabbixHostItemDto>> Handle(
        GetZabbixHostItemsQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.HostId))
            throw new ArgumentException(
                "O hostId é obrigatório.", nameof(request.HostId));

        var items = await client.ListItemsAsync(request.HostId, cancellationToken);
        var lastValues = await client.GetItemsLastValuesAsync(
            request.HostId, cancellationToken);
        var lastByItem = lastValues
            .GroupBy(v => v.ItemId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        return items
            .Select(item => new ZabbixHostItemDto(
                item.ItemId,
                item.Name,
                item.Key,
                item.Unit,
                item.Type,
                item.Enabled,
                item.Unsupported,
                lastByItem.TryGetValue(item.ItemId, out var last) ? last.LastValue : null,
                lastByItem.TryGetValue(item.ItemId, out var last2) ? last2.LastClockUtc : null))
            .ToList();
    }
}

/// <summary>Série de um item: metadados + pontos ordenados por clock asc.</summary>
public sealed record ZabbixHostSeriesDto(
    string ItemId,
    string Name,
    string Key,
    string Unit,
    IReadOnlyList<ZabbixHistoryPointDto> Points);

public sealed record ZabbixHostSeriesResultDto(
    IReadOnlyList<ZabbixHostSeriesDto> Series);

/// <summary>
/// F54-T001: histórico em lote de itens de um host (history.get com
/// itemids[]). <paramref name="ItemIds"/> é uma lista separada por vírgula
/// (máx. 20); só itens do próprio host e não-unsupported entram. O
/// <paramref name="Limit"/> é o teto TOTAL de pontos por chamada history
/// (o Zabbix limita o resultset inteiro, não por item) — janelas longas
/// com muitos itens podem exigir limite maior ou período menor.
/// </summary>
public sealed record GetZabbixHostSeriesQuery(
    string HostId,
    string? ItemIds,
    DateTime FromUtc,
    DateTime ToUtc,
    int Limit = 20000)
    : BaseQuery<ZabbixHostSeriesResultDto>, IAuthorizedRequest
{
    internal const int MaxItems = 20;
    internal const int MaxRangeDays = 92;

    public override string CacheKey =>
        $"{GetType().Name}:{HostId}:{ItemIds}:{FromUtc:O}:{ToUtc:O}:{Limit}";

    public override TimeSpan? CacheDuration => TimeSpan.FromMinutes(1);

    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Read;
}

public sealed class GetZabbixHostSeriesQueryHandler(IZabbixClient client)
    : IRequestHandler<GetZabbixHostSeriesQuery, ZabbixHostSeriesResultDto>
{
    private static readonly int[] HistoryTables = [3, 0]; // unsigned, float

    public async Task<ZabbixHostSeriesResultDto> Handle(
        GetZabbixHostSeriesQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.HostId))
            throw new ArgumentException(
                "O hostId é obrigatório.", nameof(request.HostId));

        var requestedIds = ParseItemIds(request.ItemIds);
        if (requestedIds.Count == 0)
            throw new ArgumentException(
                "Informe ao menos um itemId.", nameof(request.ItemIds));

        if (request.ToUtc < request.FromUtc)
            throw new ArgumentException(
                "O período final deve ser maior ou igual ao inicial.",
                nameof(request.ToUtc));

        if (request.ToUtc - request.FromUtc > TimeSpan.FromDays(GetZabbixHostSeriesQuery.MaxRangeDays))
            throw new ArgumentException(
                $"O período máximo por consulta é de {GetZabbixHostSeriesQuery.MaxRangeDays} dias.",
                nameof(request.FromUtc));

        if (request.Limit is < 1 or > 100000)
            throw new ArgumentException(
                "O limite deve estar entre 1 e 100000.", nameof(request.Limit));

        // Resolve os itens do host: só o que pertence ao host e está
        // supported entra na série (itemid arbitrário de outro host é
        // ignorado — a rota é do host).
        var hostItems = await client.ListItemsAsync(request.HostId, cancellationToken);
        var selected = hostItems
            .Where(item =>
                item.HostId == request.HostId
                && requestedIds.Contains(item.ItemId)
                && !item.Unsupported)
            .ToList();

        if (selected.Count == 0)
            return new ZabbixHostSeriesResultDto([]);

        var itemIds = selected.Select(i => i.ItemId).ToList();
        var points = new List<ZabbixHistoryValueDto>();
        foreach (var history in HistoryTables)
        {
            points.AddRange(await client.GetHistoryBatchAsync(
                itemIds, history, request.FromUtc, request.ToUtc,
                request.Limit, cancellationToken));
        }

        // Um item só existe em uma das tabelas de histórico — dedup por
        // (item, clock) blinda contra sobreposição.
        var deduped = points
            .GroupBy(p => (p.ItemId, p.ClockUtc))
            .Select(g => g.First());

        var series = selected
            .Select(item => new ZabbixHostSeriesDto(
                item.ItemId,
                item.Name,
                item.Key,
                item.Unit,
                deduped
                    .Where(p => p.ItemId == item.ItemId)
                    .OrderBy(p => p.ClockUtc)
                    .Select(p => new ZabbixHistoryPointDto(p.ClockUtc, p.Value))
                    .ToList()))
            .ToList();

        return new ZabbixHostSeriesResultDto(series);
    }

    private static HashSet<string> ParseItemIds(string? itemIds)
    {
        var ids = (itemIds ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (ids.Count > GetZabbixHostSeriesQuery.MaxItems)
            throw new ArgumentException(
                $"No máximo {GetZabbixHostSeriesQuery.MaxItems} itens por consulta.",
                nameof(itemIds));

        return ids.ToHashSet(StringComparer.Ordinal);
    }
}
