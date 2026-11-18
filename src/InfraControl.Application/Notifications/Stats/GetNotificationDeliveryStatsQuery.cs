using InfraControl.Application.Notifications.Stats.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Common;
using InfraControl.Domain.Enums;
using MediatR;

namespace InfraControl.Application.Notifications.Stats;

/// <summary>Ponto agregado: um dia (yyyy-MM-dd, fuso de Brasília) × canal × status.</summary>
public sealed record NotificationDeliveryStatPointDto(
    string Date,
    string Channel,
    string Status,
    int Count);

public sealed record NotificationDeliveryStatsDto(
    IReadOnlyList<NotificationDeliveryStatPointDto> Points,
    int Days);

/// <summary>
/// Entregas de notificação por dia/canal/status — alimenta os gráficos das
/// telas de WhatsApp, E-mail e Integrações. Janela em dias (1–90, padrão 14),
/// dias contados no fuso de Brasília.
/// </summary>
public sealed record GetNotificationDeliveryStatsQuery(int Days = 14)
    : BaseQuery<NotificationDeliveryStatsDto>, IAuthorizedRequest
{
    public int NormalizedDays => Days is > 0 and <= 90 ? Days : 14;

    public override TimeSpan? CacheDuration => TimeSpan.FromSeconds(30);

    public override IReadOnlyCollection<string> CacheTags => ["notifications"];

    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

public sealed class GetNotificationDeliveryStatsQueryHandler(
    INotificationDeliveryStatsStore stats)
    : IRequestHandler<GetNotificationDeliveryStatsQuery, NotificationDeliveryStatsDto>
{
    private static readonly TimeZoneInfo SaoPaulo =
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public async Task<NotificationDeliveryStatsDto> Handle(
        GetNotificationDeliveryStatsQuery request, CancellationToken cancellationToken)
    {
        var days = request.NormalizedDays;
        var sinceUtc = AppTimeProvider.Current.GetUtcNow().UtcDateTime
            .AddDays(-days);

        var rows = await stats.ListSinceAsync(sinceUtc, cancellationToken);

        var points = rows
            .GroupBy(row => (
                Day: ToSaoPauloDate(row.CreateDateTimeUtc),
                Channel: row.Channel.ToString(),
                Status: row.Status.ToString()))
            .Select(group => new NotificationDeliveryStatPointDto(
                group.Key.Day.ToString("yyyy-MM-dd"),
                group.Key.Channel,
                group.Key.Status,
                group.Count()))
            .OrderBy(point => point.Date)
            .ThenBy(point => point.Channel)
            .ToList();

        return new NotificationDeliveryStatsDto(points, days);
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
