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
    DateTime? ResolvedAt);
