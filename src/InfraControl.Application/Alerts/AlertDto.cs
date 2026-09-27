using InfraControl.Application.Alerts.Ports;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Alerts;

/// <summary>Alerta com o device resolvido (F6-T001).</summary>
public sealed record AlertDto(
    Guid Id,
    Guid DeviceId,
    string DeviceName,
    string Check,
    string Severity,
    string Status,
    string Message,
    DateTime OpenedAt,
    DateTime? ResolvedAt);
