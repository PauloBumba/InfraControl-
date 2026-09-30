using InfraControl.Application.Incidents.Ports;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Integrations.Zabbix;

// F16-T001/ADR-023: ingestão do webhook do Zabbix. O comando NÃO implementa
// IAuthorizedRequest de propósito — o guard é o token X-Zabbix-Token no
// controller (AuthorizationBehavior pula requests não-autorizados) e a
// allowlist Admin_Slices do arch test continua intacta. Erros de validação
// nascem aqui em pt-BR (ArgumentException → 400 pelo GlobalExceptionHandler).

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
/// PROBLEM cria incidente (device opcional — decisão do usuário), OK resolve
/// o correspondente; dedup anti-storm por título+device (heurística F6).
/// Severity 0..5 (Not classified..Disaster); status PROBLEM|OK.
/// </summary>
public sealed record IngestZabbixProblemCommand(
    string? EventId,
    string? Host,
    string? Ip,
    string Name,
    int? Severity,
    string Status) : BaseCommand<ZabbixProblemResultDto>;

public sealed class IngestZabbixProblemCommandHandler(
    IIncidentRepository incidentRepository,
    IDeviceRepository deviceRepository,
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

        var title = name.Trim();
        var device = await ResolveDeviceAsync(
            request.Ip, request.Host, cancellationToken);

        if (status == "OK")
        {
            var open = await incidentRepository.GetOpenByTitleAndDeviceAsync(
                title, device?.Id, cancellationToken);
            if (open is null)
            {
                logger.LogInformation(
                    "Zabbix OK sem incidente aberto (evento {EventId}, título {Title}).",
                    request.EventId, title);
                return new(null, ZabbixProblemResultDto.None);
            }

            open.ChangeStatus(IncidentStatus.Resolved);
            await incidentRepository.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Zabbix OK resolveu o incidente {IncidentId} (evento {EventId}).",
                open.Id, request.EventId);
            return new(open.Id, ZabbixProblemResultDto.Resolved);
        }

        // PROBLEM: mesmo título + mesmo device aberto = idempotente (anti-storm)
        var existing = await incidentRepository.GetOpenByTitleAndDeviceAsync(
            title, device?.Id, cancellationToken);
        if (existing is not null)
        {
            logger.LogInformation(
                "Zabbix PROBLEM duplicado (evento {EventId}) — incidente {IncidentId} já aberto.",
                request.EventId, existing.Id);
            return new(existing.Id, ZabbixProblemResultDto.Duplicate);
        }

        var incident = Incident.Create(
            title,
            MapSeverity(severity.Value),
            device?.Id,
            BuildDescription(request.EventId, request.Host, request.Ip));

        await incidentRepository.AddAsync(incident, cancellationToken);
        await incidentRepository.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Incident {IncidentId} criado a partir do Zabbix (evento {EventId}, status {Status}).",
            incident.Id, request.EventId, status);

        return new(incident.Id, ZabbixProblemResultDto.Created);
    }

    /// <summary>
    /// Device por IP exato (F4-T010) com fallback por hostname; null = sem
    /// match no inventário (incidente nasce sem device — decisão do usuário).
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

    /// <summary>Severidade Zabbix (6 níveis) → IncidentSeverity (4 da UI).</summary>
    internal static IncidentSeverity MapSeverity(int severity) => severity switch
    {
        0 or 1 => IncidentSeverity.Low,
        2 => IncidentSeverity.Medium,
        3 => IncidentSeverity.High,
        _ => IncidentSeverity.Critical
    };

    /// <summary>Origem do evento para auditoria humana (partes presentes).</summary>
    private static string? BuildDescription(string? eventId, string? host, string? ip)
    {
        var parts = new List<string>(3);
        if (!string.IsNullOrWhiteSpace(eventId))
            parts.Add($"evento {eventId.Trim()}");
        if (!string.IsNullOrWhiteSpace(host))
            parts.Add($"host {host.Trim()}");
        if (!string.IsNullOrWhiteSpace(ip))
            parts.Add($"ip {ip.Trim()}");

        return parts.Count == 0
            ? null
            : $"Recebido do Zabbix — {string.Join(", ", parts)}.";
    }
}
