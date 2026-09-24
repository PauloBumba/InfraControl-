using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Instância de check por device (F5-T001) — nasce do DeviceProfile
/// (ADR-014 §4: F5-T001 cria <c>MonitoringCheck</c> e resolve o provider).
/// Sincronizada nos handlers de create e de update de perfil; linha perdida
/// quando o device é excluído (FK cascade). Índice único (DeviceId, Kind).
/// </summary>
public sealed class MonitoringCheck : AggregateRoot
{
    public Guid DeviceId { get; private set; }
    public CheckKind Kind { get; private set; }
    public bool Enabled { get; private set; }

    private MonitoringCheck() { }

    public static MonitoringCheck Create(Guid deviceId, CheckKind kind, bool enabled = true)
    {
        if (deviceId == Guid.Empty)
            throw new ArgumentException("O id do device é obrigatório.", nameof(deviceId));

        return new MonitoringCheck
        {
            DeviceId = deviceId,
            Kind = kind,
            Enabled = enabled
        };
    }
}
