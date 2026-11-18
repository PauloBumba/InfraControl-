using InfraControl.Application.Monitoring.Events.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Common;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Integrations.Zabbix;

/// <summary>Ponto agregado: um dia (yyyy-MM-dd, fuso de Brasília) × severidade × status.</summary>
public sealed record ZabbixEventStatPointDto(
    string Date,
    int Severity,
    string Status,
    int Count);

public sealed record ZabbixEventStatsDto(
    IReadOnlyList<ZabbixEventStatPointDto> Points,
    int Days);

/// <summary>
/// Eventos Zabbix ingeridos por dia/severidade/status — gráfico da tela de
/// integração. Janela em dias (1–90, padrão 14), dias no fuso de Brasília.
/// </summary>
public sealed record GetZabbixEventStatsQuery(int Days = 14)
    : BaseQuery<ZabbixEventStatsDto>, IAuthorizedRequest
{
    public int NormalizedDays => Days is > 0 and <= 90 ? Days : 14;

    public override TimeSpan? CacheDuration => TimeSpan.FromSeconds(30);

    public override IReadOnlyCollection<string> CacheTags => ["monitoring"];

    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class GetZabbixEventStatsQueryHandler(
    IMonitoringEventRepository events)
    : IRequestHandler<GetZabbixEventStatsQuery, ZabbixEventStatsDto>
{
    private static readonly TimeZoneInfo SaoPaulo =
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public async Task<ZabbixEventStatsDto> Handle(
        GetZabbixEventStatsQuery request, CancellationToken cancellationToken)
    {
        var days = request.NormalizedDays;
        var sinceUtc = AppTimeProvider.Current.GetUtcNow().UtcDateTime
            .AddDays(-days);

        var rows = await events.ListStatsSinceAsync(
            MonitoringSource.Zabbix, sinceUtc, cancellationToken);

        var points = rows
            .GroupBy(row => (
                Day: ToSaoPauloDate(row.OccurredAtUtc),
                row.Severity,
                Status: row.Status.ToString()))
            .Select(group => new ZabbixEventStatPointDto(
                group.Key.Day.ToString("yyyy-MM-dd"),
                group.Key.Severity,
                group.Key.Status,
                group.Count()))
            .OrderBy(point => point.Date)
            .ThenBy(point => point.Severity)
            .ToList();

        return new ZabbixEventStatsDto(points, days);
    }

    private static DateOnly ToSaoPauloDate(DateTime utc)
    {
        var utcDate = utc.Kind == DateTimeKind.Utc
            ? utc
            : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return DateOnly.FromDateTime(
            TimeZoneInfo.ConvertTimeFromUtc(utcDate, SaoPaulo));
    }
}
