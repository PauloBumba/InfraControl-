using InfraControl.Application.Alerts.Ports;
using InfraControl.Application.Incidents.Ports;
using InfraControl.Application.Monitoring.Results.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Common;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Dashboard.Queries;

// F54-T003 (docs/69 §2.2): séries temporais do dashboard — polls
// (total/falha), alertas (abertos/resolvidos) e incidentes
// (abertos/resolvidos) alinhados em buckets para os gráficos da página.
// Janela semi-aberta [from, to); buckets zerados quando não há dado
// (a linha do tempo continua contínua). Período inválido → ArgumentException
// (400 pt-BR pelo GlobalExceptionHandler). Cache de 1min com a chave
// distinta por período.

/// <summary>Ponto de série (bucket UTC + valor).</summary>
public sealed record DashboardSeriesPointDto(DateTime BucketUtc, int Value);

public sealed record DashboardSeriesDto(
    string Period,
    DateTime FromUtc,
    DateTime ToUtc,
    int BucketMinutes,
    IReadOnlyList<DashboardSeriesPointDto> Polls,
    IReadOnlyList<DashboardSeriesPointDto> PollFailures,
    IReadOnlyList<DashboardSeriesPointDto> AlertsOpened,
    IReadOnlyList<DashboardSeriesPointDto> AlertsResolved,
    IReadOnlyList<DashboardSeriesPointDto> IncidentsOpened,
    IReadOnlyList<DashboardSeriesPointDto> IncidentsResolved);

public sealed record GetDashboardSeriesQuery(string Period)
    : BaseQuery<DashboardSeriesDto>, IAuthorizedRequest
{
    public override string CacheKey => $"{GetType().Name}:{Period}";

    public override TimeSpan? CacheDuration => TimeSpan.FromMinutes(1);

    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Read;
}

public sealed class GetDashboardSeriesQueryHandler(
    IMonitoringResultRepository monitoringResultRepository,
    IAlertRepository alertRepository,
    IIncidentRepository incidentRepository)
    : IRequestHandler<GetDashboardSeriesQuery, DashboardSeriesDto>
{
    private sealed record PeriodSpec(
        TimeSpan Duration, int BucketMinutes, bool MinuteGranularity);

    // Períodos pedidos na spec (§3): última hora, 24 horas, 7 e 30 dias.
    // Granularidade: minuto na hora (buckets de5min), hora nos demais
    // (buckets de1h/6h/1d) — o máximo de linhas agregadas é720 (30d).
    private static readonly IReadOnlyDictionary<string, PeriodSpec> Periods =
        new Dictionary<string, PeriodSpec>(StringComparer.Ordinal)
        {
            ["1h"] = new(TimeSpan.FromHours(1), 5, MinuteGranularity: true),
            ["24h"] = new(TimeSpan.FromHours(24), 60, MinuteGranularity: false),
            ["7d"] = new(TimeSpan.FromDays(7), 360, MinuteGranularity: false),
            ["30d"] = new(TimeSpan.FromDays(30), 1440, MinuteGranularity: false)
        };

    public async Task<DashboardSeriesDto> Handle(
        GetDashboardSeriesQuery request, CancellationToken cancellationToken)
    {
        if (!Periods.TryGetValue(request.Period ?? string.Empty, out var spec))
            throw new ArgumentException(
                $"Período inválido '{request.Period}'. Use 1h, 24h, 7d ou 30d.",
                nameof(request.Period));

        var toUtc = AppTimeProvider.Current.GetUtcNow().UtcDateTime;
        var fromUtc = toUtc - spec.Duration;

        var polls = spec.MinuteGranularity
            ? await monitoringResultRepository.GetPollCountsByMinuteAsync(
                fromUtc, toUtc, cancellationToken)
            : await monitoringResultRepository.GetPollCountsByHourAsync(
                fromUtc, toUtc, cancellationToken);
        var alertRows = await alertRepository.ListTimestampsBetweenAsync(
            fromUtc, toUtc, cancellationToken);
        var incidentRows = await incidentRepository.ListTimestampsBetweenAsync(
            fromUtc, toUtc, cancellationToken);

        // Buckets alinhados ao relógio e zerados (rótulos UTC).
        var buckets = new List<DateTime>();
        for (var start = BucketStart(fromUtc, spec.BucketMinutes);
             start < toUtc;
             start = start.AddMinutes(spec.BucketMinutes))
            buckets.Add(start);

        var indexOf = new Dictionary<DateTime, int>(buckets.Count);
        for (var i = 0; i < buckets.Count; i++)
            indexOf[buckets[i]] = i;

        var pollTotal = new int[buckets.Count];
        var pollFailed = new int[buckets.Count];
        var alertsOpened = new int[buckets.Count];
        var alertsResolved = new int[buckets.Count];
        var incidentsOpened = new int[buckets.Count];
        var incidentsResolved = new int[buckets.Count];

        foreach (var row in polls)
            if (indexOf.TryGetValue(BucketStart(row.BucketUtc, spec.BucketMinutes), out var index))
            {
                pollTotal[index] += row.Total;
                pollFailed[index] += row.Failed;
            }

        foreach (var row in alertRows)
        {
            if (indexOf.TryGetValue(
                    BucketStart(row.CreatedAt, spec.BucketMinutes), out var opened))
                alertsOpened[opened]++;
            if (row.ResolvedAt is { } resolved && indexOf.TryGetValue(
                    BucketStart(resolved, spec.BucketMinutes), out var closed))
                alertsResolved[closed]++;
        }

        foreach (var row in incidentRows)
        {
            if (indexOf.TryGetValue(
                    BucketStart(row.CreatedAt, spec.BucketMinutes), out var opened))
                incidentsOpened[opened]++;
            if (row.ResolvedAt is { } resolved && indexOf.TryGetValue(
                    BucketStart(resolved, spec.BucketMinutes), out var closed))
                incidentsResolved[closed]++;
        }

        return new DashboardSeriesDto(
            request.Period,
            fromUtc,
            toUtc,
            spec.BucketMinutes,
            Points(buckets, pollTotal),
            Points(buckets, pollFailed),
            Points(buckets, alertsOpened),
            Points(buckets, alertsResolved),
            Points(buckets, incidentsOpened),
            Points(buckets, incidentsResolved));
    }

    private static IReadOnlyList<DashboardSeriesPointDto> Points(
        IReadOnlyList<DateTime> buckets, int[] values)
    {
        var points = new DashboardSeriesPointDto[buckets.Count];
        for (var i = 0; i < buckets.Count; i++)
            points[i] = new DashboardSeriesPointDto(buckets[i], values[i]);
        return points;
    }

    /// <summary>
    /// Início do bucket no relógio (5min/1h/6h/1d alinhados ao calendário) —
    /// mesma grade usada para agrupar as linhas vindas do repositório.
    /// </summary>
    private static DateTime BucketStart(DateTime moment, int bucketMinutes)
    {
        if (bucketMinutes >= 1440)
            return new DateTime(
                moment.Year, moment.Month, moment.Day, 0, 0, 0, DateTimeKind.Utc);

        if (bucketMinutes >= 60)
        {
            var hours = bucketMinutes / 60;
            return new DateTime(
                moment.Year, moment.Month, moment.Day,
                moment.Hour - moment.Hour % hours, 0, 0, DateTimeKind.Utc);
        }

        return new DateTime(
            moment.Year, moment.Month, moment.Day, moment.Hour,
            moment.Minute - moment.Minute % bucketMinutes, 0, DateTimeKind.Utc);
    }
}
