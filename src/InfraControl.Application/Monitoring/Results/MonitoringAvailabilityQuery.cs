using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Monitoring.Results.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Monitoring.Results;

// F54-T001: série de disponibilidade dos checks internos (monitoring_results)
// para o gráfico do dashboard — "% de polls com sucesso" por bucket, dado
// real e independente do Zabbix. O repositório agrega em células de 5 min
// no banco; aqui as células são fundidas nos buckets pedidos (sempre
// múltiplos de 5 min, alinhados a partir de FromUtc).

/// <summary>Bucket da série: polls totais/com sucesso no intervalo.</summary>
public sealed record MonitoringAvailabilityPointDto(
    DateTime BucketStartUtc,
    int Total,
    int Succeeded,
    int Failed);

public sealed record MonitoringAvailabilityResultDto(
    DateTime FromUtc,
    DateTime ToUtc,
    int BucketMinutes,
    IReadOnlyList<MonitoringAvailabilityPointDto> Points);

/// <summary>
/// Disponibilidade dos checks internos num período. <paramref name="BucketMinutes"/>
/// opcional: múltiplo de 5 entre 5 e 1440; quando omitido, o handler escolhe
/// ~80 pontos (~ range/80 arredondado para 5 min). Período máximo 92 dias
/// e no máximo 2000 buckets por consulta.
/// </summary>
public sealed record GetMonitoringAvailabilityQuery(
    DateTime? FromUtc,
    DateTime? ToUtc,
    int? BucketMinutes)
    : BaseQuery<MonitoringAvailabilityResultDto>, IAuthorizedRequest
{
    internal const int MinBucketMinutes = 5;
    internal const int MaxBucketMinutes = 1440;
    internal const int MaxRangeDays = 92;
    internal const int MaxPoints = 2000;
    internal const int DefaultRangeHours = 24;

    public override string CacheKey =>
        $"{GetType().Name}:{FromUtc:O}:{ToUtc:O}:{BucketMinutes}";

    public override TimeSpan? CacheDuration => TimeSpan.FromSeconds(30);

    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Read;
}

public sealed class GetMonitoringAvailabilityQueryHandler(
    IMonitoringResultRepository resultRepository)
    : IRequestHandler<GetMonitoringAvailabilityQuery, MonitoringAvailabilityResultDto>
{
    public async Task<MonitoringAvailabilityResultDto> Handle(
        GetMonitoringAvailabilityQuery request, CancellationToken cancellationToken)
    {
        var toUtc = request.ToUtc ?? DateTime.UtcNow;
        var fromUtc = request.FromUtc ?? toUtc.AddHours(-GetMonitoringAvailabilityQuery.DefaultRangeHours);

        if (toUtc < fromUtc)
            throw new ArgumentException(
                "O período final deve ser maior ou igual ao inicial.",
                nameof(request.ToUtc));

        var range = toUtc - fromUtc;
        if (range > TimeSpan.FromDays(GetMonitoringAvailabilityQuery.MaxRangeDays))
            throw new ArgumentException(
                $"O período máximo por consulta é de {GetMonitoringAvailabilityQuery.MaxRangeDays} dias.",
                nameof(request.FromUtc));

        var bucketMinutes = ResolveBucketMinutes(request.BucketMinutes, range);
        var bucketCount = range <= TimeSpan.Zero
            ? 0
            : (int)Math.Ceiling(range.TotalMinutes / bucketMinutes);
        if (bucketCount > GetMonitoringAvailabilityQuery.MaxPoints)
            throw new ArgumentException(
                $"O período/bucket geraria mais de {GetMonitoringAvailabilityQuery.MaxPoints} pontos; aumente o bucket.",
                nameof(request.BucketMinutes));

        var cells = await resultRepository.GetAvailabilityCellsAsync(
            fromUtc, toUtc, cancellationToken);

        var points = Enumerable
            .Range(0, bucketCount)
            .Select(i => new MonitoringAvailabilityPointDto(
                fromUtc.AddMinutes(i * bucketMinutes), 0, 0, 0))
            .ToList();

        var cellTicks = TimeSpan.FromMinutes(bucketMinutes).Ticks;
        var origin = fromUtc.Ticks;
        foreach (var cell in cells)
        {
            var index = (int)((cell.StartUtc.Ticks - origin) / cellTicks);
            if (index < 0 || index >= points.Count)
                continue;
            var point = points[index];
            points[index] = point with
            {
                Total = point.Total + cell.Total,
                Succeeded = point.Succeeded + cell.Succeeded,
                Failed = point.Failed + (cell.Total - cell.Succeeded),
            };
        }

        return new MonitoringAvailabilityResultDto(
            fromUtc, toUtc, bucketMinutes, points);
    }

    private static int ResolveBucketMinutes(int? requested, TimeSpan range)
    {
        int bucket;
        if (requested is { } value)
        {
            if (value < GetMonitoringAvailabilityQuery.MinBucketMinutes
                || value > GetMonitoringAvailabilityQuery.MaxBucketMinutes
                || value % GetMonitoringAvailabilityQuery.MinBucketMinutes != 0)
                throw new ArgumentException(
                    $"O bucket deve ser múltiplo de {GetMonitoringAvailabilityQuery.MinBucketMinutes} entre {GetMonitoringAvailabilityQuery.MinBucketMinutes} e {GetMonitoringAvailabilityQuery.MaxBucketMinutes} minutos.",
                    nameof(requested));
            bucket = value;
        }
        else
        {
            // ~80 pontos: arredonda para cima em múltiplos de 5 min.
            var raw = range.TotalMinutes / 80.0;
            bucket = Math.Clamp(
                (int)Math.Ceiling(raw / GetMonitoringAvailabilityQuery.MinBucketMinutes)
                    * GetMonitoringAvailabilityQuery.MinBucketMinutes,
                GetMonitoringAvailabilityQuery.MinBucketMinutes,
                GetMonitoringAvailabilityQuery.MaxBucketMinutes);
        }
        return bucket;
    }
}
