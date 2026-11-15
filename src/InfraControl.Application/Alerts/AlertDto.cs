using InfraControl.Application.Alerts.Ports;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Alerts;

/// <summary>
/// Alerta com o device resolvido (F6-T001). ADR-027: DeviceId/DeviceName/
/// Check nullable — alerta externo de host não mapeado não tem device nem
/// CheckKind nativo; Source = Native|Zabbix.
/// </summary>
public sealed record AlertDto(
    Guid Id,
    Guid? DeviceId,
    string? DeviceName,
    string? Check,
    string Severity,
    string Status,
    string Source,
    string Message,
    DateTime OpenedAt,
    DateTime? ResolvedAt,
    string? OperatorNote = null,
    // F52-T003 — baixa justificada (null na resolução automática).
    string? ResolutionCode = null,
    string? ResolutionNote = null,
    // Responsável do alerta (atribuído direto) ou, na falta dele, do
    // incidente vinculado — quem está tratando.
    string? AssignedToName = null,
    Guid? AssignedToId = null,
    // Incidente vinculado (N:N via IncidentAlert) — alerta ≠ incidente;
    // o link deixa a relação explícita na listagem.
    Guid? LinkedIncidentId = null,
    string? LinkedIncidentTitle = null)
{
    /// <summary>DTO a partir da entidade (com device resolvido opcional).</summary>
    public static AlertDto From(
        Domain.Entities.Alert alert,
        string? deviceName = null,
        string? assignedToName = null,
        Guid? linkedIncidentId = null,
        string? linkedIncidentTitle = null) =>
        new(
            alert.Id,
            alert.DeviceId,
            deviceName,
            alert.Kind?.ToString(),
            alert.Severity.ToString(),
            alert.Status.ToString(),
            alert.Source.ToString(),
            alert.Message,
            alert.CreateDateTime,
            alert.ResolvedAt,
            alert.OperatorNote,
            alert.ResolutionCode?.ToString(),
            alert.ResolutionNote,
            assignedToName,
            alert.AssignedToId,
            linkedIncidentId,
            linkedIncidentTitle);
}
