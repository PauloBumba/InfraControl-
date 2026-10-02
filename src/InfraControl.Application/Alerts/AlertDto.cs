using InfraControl.Application.Alerts.Ports;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.Alerts;

/// <summary>
/// Alerta com o device resolvido (F6-T001). `DeviceId` é nulo para alertas
/// de fonte externa (ADR-027) cujo host não mapeia device nativo — a
/// entidade virou `Guid?` no preparo do F19-T002.
/// </summary>
public sealed record AlertDto(
    Guid Id,
    Guid? DeviceId,
    string DeviceName,
    string Check,
    string Severity,
    string Status,
    string Message,
    DateTime OpenedAt,
    DateTime? ResolvedAt);
