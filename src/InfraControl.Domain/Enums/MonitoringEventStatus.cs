namespace InfraControl.Domain.Enums;

/// <summary>
/// Ciclo de vida de um MonitoringEvent (ADR-027): nasce Problem e vai para
/// Recovered quando o evento de recuperação da fonte chega (Zabbix: problem
/// eventid 100 → recovery eventid 150) — um row por evento recebido, com
/// correlação de lifecycle em <c>ExternalProblemId</c>.
/// </summary>
public enum MonitoringEventStatus
{
    Problem = 0,
    Recovered = 1
}
