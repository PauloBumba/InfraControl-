using System.Text.Json;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Integrations.Zabbix.Ports;
using InfraControl.Application.Monitoring.Events;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Integrations.Zabbix;

// F16-T001/ADR-023/ADR-027: ingestão do webhook do Zabbix. O comando NÃO
// implementa IAuthorizedRequest de propósito — o guard é o token X-Zabbix-Token
// no controller (AuthorizationBehavior pula requests não-autorizados) e a
// allowlist Admin_Slices do arch test continua intacta. Erros de validação
// nascem aqui em pt-BR (ArgumentException → 400 pelo GlobalExceptionHandler).
// ADR-027: o handler valida/normaliza (contrato docs/27) e delega o ciclo
// Event→Alert→Incident ao MonitoringEventIngestor — sem dedup por título.

/// <summary>
/// Resultado da ingestão: incidente afetado (ou null) + outcome em token
/// (created|duplicate|resolved|none) — contrato documentado em docs/27.
/// </summary>
public sealed record ZabbixProblemResultDto(Guid? IncidentId, string Outcome)
{
    public const string Created = "created";
    public const string Duplicate = "duplicate";
    public const string Resolved = "resolved";
    public const string None = "none";
}

/// <summary>
/// Evento do Zabbix montado no action (macros em docs/27):
/// PROBLEM cria MonitoringEvent+Alert+Incident (device opcional — decisão do
/// usuário), OK recupera o problem original. Chave = eventId (eventid do
/// Zabbix); recoveryEventId ({EVENT.RECOVERY.ID}) habilita a linha própria de
/// recovery. Severity 0..5 (Not classified..Disaster); status PROBLEM|OK.
/// </summary>
public sealed record IngestZabbixProblemCommand(
    string? EventId,
    string? RecoveryEventId,
    string? Host,
    string? Ip,
    string Name,
    int? Severity,
    string Status,
    long? OccurredAt) : BaseCommand<ZabbixProblemResultDto>;

public sealed class IngestZabbixProblemCommandHandler(
    IMonitoringEventIngestor ingestor,
    IDeviceRepository deviceRepository,
    IZabbixEventContextReader eventContextReader,
    ILogger<IngestZabbixProblemCommandHandler> logger)
    : IRequestHandler<IngestZabbixProblemCommand, ZabbixProblemResultDto>
{
    public async Task<ZabbixProblemResultDto> Handle(
        IngestZabbixProblemCommand request, CancellationToken cancellationToken)
    {
        // validação name → severity → status (ordem estável, mensagem observável)
        var name = request.Name;
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "O nome do problema é obrigatório.", nameof(name));

        var severity = request.Severity;
        if (severity is null)
            throw new ArgumentException(
                "A severidade é obrigatória (0 a 5).", nameof(severity));
        if (severity is < 0 or > 5)
            throw new ArgumentException(
                $"Severidade inválida: {severity}. Use 0 (Not classified) a 5 (Disaster).",
                nameof(severity));

        var status = request.Status?.Trim().ToUpperInvariant() ?? string.Empty;
        if (status is not ("PROBLEM" or "OK"))
            throw new ArgumentException(
                $"Status inválido: '{request.Status}'. Use 'PROBLEM' ou 'OK'.",
                nameof(status));

        // eventId = chave de dedup (Source, ExternalEventId) — obrigatório nos
        // dois status (no OK é o eventid do problem original: {EVENT.ID}).
        var eventId = request.EventId?.Trim();
        if (string.IsNullOrWhiteSpace(eventId))
            throw new ArgumentException(
                "O eventId é obrigatório (eventid do Zabbix — dedup e correlação).",
                nameof(request.EventId));
        if (eventId.Length > MonitoringEvent.ExternalIdMaxLength)
            throw new ArgumentException(
                $"O eventId excede {MonitoringEvent.ExternalIdMaxLength} caracteres.",
                nameof(request.EventId));

        var recoveryId = request.RecoveryEventId?.Trim();
        if (string.IsNullOrEmpty(recoveryId))
            recoveryId = null;
        else if (recoveryId.Length > MonitoringEvent.ExternalIdMaxLength)
            throw new ArgumentException(
                $"O recoveryEventId excede {MonitoringEvent.ExternalIdMaxLength} caracteres.",
                nameof(request.RecoveryEventId));

        var title = name.Trim();
        var device = await ResolveDeviceAsync(
            request.Ip, request.Host, cancellationToken);
        var sourceContext = await eventContextReader.GetAsync(
            eventId, cancellationToken);

        var input = new ZabbixIngestInput(
            eventId,
            recoveryId,
            device?.Id,
            request.Host,
            request.Ip,
            title,
            severity.Value,
            IsRecovery: status == "OK",
            ToUtcDateTime(request.OccurredAt),
            BuildRawPayload(request),
            sourceContext is null ? null : JsonSerializer.Serialize(sourceContext));

        var result = await ingestor.IngestAsync(input, cancellationToken);

        switch (result.Outcome)
        {
            case IngestOutcome.Duplicate:
                logger.LogInformation(
                    "Zabbix PROBLEM duplicado (evento {EventId}) — incidente {IncidentId} já existe.",
                    eventId, result.IncidentId);
                break;
            case IngestOutcome.Resolved:
                logger.LogInformation(
                    "Zabbix OK resolveu o incidente {IncidentId} (evento {EventId}).",
                    result.IncidentId, eventId);
                break;
            case IngestOutcome.None:
                logger.LogInformation(
                    "Zabbix OK sem correspondência (evento {EventId}, título {Title}).",
                    eventId, title);
                break;
        }

        return new(result.IncidentId, MapOutcome(result.Outcome));
    }

    private static string MapOutcome(IngestOutcome outcome) => outcome switch
    {
        IngestOutcome.Created => ZabbixProblemResultDto.Created,
        IngestOutcome.Duplicate => ZabbixProblemResultDto.Duplicate,
        IngestOutcome.Resolved => ZabbixProblemResultDto.Resolved,
        _ => ZabbixProblemResultDto.None
    };

    /// <summary>
    /// Device por IP exato (F4-T010) com fallback por hostname; null = sem
    /// match no inventário (alerta/incidente nascem sem device — decisão do
    /// usuário).
    /// </summary>
    private async Task<Device?> ResolveDeviceAsync(
        string? ip, string? host, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(ip))
        {
            var byIp = await deviceRepository.GetByIpAsync(
                ip.Trim(), cancellationToken);
            if (byIp is not null)
                return byIp;
        }

        if (!string.IsNullOrWhiteSpace(host))
            return await deviceRepository.GetByNameAsync(
                host.Trim(), cancellationToken);

        return null;
    }

    /// <summary>
    /// {EVENT.TIMESTAMP} (unix seconds) → UTC; ausente/zero/absurdo
    /// (&gt; 2100-01-01) = null (o ingestor usa o relógio da plataforma).
    /// </summary>
    private static DateTime? ToUtcDateTime(long? unixSeconds)
    {
        if (unixSeconds is null or <= 0 or > 4102444800L)
            return null;

        return DateTimeOffset.FromUnixTimeSeconds(unixSeconds.Value).UtcDateTime;
    }

    /// <summary>Payload reconstruído (RawPayload do MonitoringEvent).</summary>
    private static string BuildRawPayload(IngestZabbixProblemCommand request)
        => JsonSerializer.Serialize(new
        {
            eventId = request.EventId,
            recoveryEventId = request.RecoveryEventId,
            host = request.Host,
            ip = request.Ip,
            name = request.Name,
            severity = request.Severity,
            status = request.Status,
            occurredAt = request.OccurredAt
        });
}
