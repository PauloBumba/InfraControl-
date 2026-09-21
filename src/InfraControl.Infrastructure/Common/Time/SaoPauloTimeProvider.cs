namespace InfraControl.Infrastructure.Common.Time;

/// <summary>
/// TimeProvider that returns time in São Paulo timezone (UTC-3).
/// Uses America/Sao_Paulo IANA timezone.
/// </summary>
public sealed class SaoPauloTimeProvider : TimeProvider
{
    private static readonly TimeZoneInfo SaoPauloTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public override TimeZoneInfo LocalTimeZone => SaoPauloTimeZone;

    public override DateTimeOffset GetUtcNow()
    {
        var utcNow = base.GetUtcNow();
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(utcNow.UtcDateTime, SaoPauloTimeZone);
        return new DateTimeOffset(localTime, SaoPauloTimeZone.GetUtcOffset(localTime));
    }
}
