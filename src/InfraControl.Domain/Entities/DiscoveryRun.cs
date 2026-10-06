using InfraControl.Domain.Common;
using InfraControl.Domain.Common.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;

namespace InfraControl.Domain.Entities;

/// <summary>
/// Execução de discovery contra UM device alvo (docs/03, F4-T001). Fluxo
/// decidido pelo usuário: gatilho MANUAL por device (agendamento depois).
/// A T001 entrega estrutura + leitura — o POST/gatilho e as chamadas de
/// Complete/Fail vêm com os providers (F4-T003 ICMP, F4-T004 SNMP, ...).
/// Histórico é perdido quando o device é excluído (FK cascade — mesma
/// decisão do MonitoringCheck).
/// </summary>
public sealed class DiscoveryRun : AggregateRoot
{
    public const int ErrorMessageMaxLength = 500;

    public Guid DeviceId { get; private set; }
    public DiscoveryRunStatus Status { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public string? ErrorMessage { get; private set; }

    private DiscoveryRun() { }

    public static DiscoveryRun Start(Guid deviceId, DateTime? startedAt = null)
    {
        if (deviceId == Guid.Empty)
            throw new ArgumentException("O id do device é obrigatório.", nameof(deviceId));

        var run = new DiscoveryRun
        {
            DeviceId = deviceId,
            Status = DiscoveryRunStatus.Running,
            StartedAt = startedAt ?? AppTimeProvider.Current.GetUtcNow().UtcDateTime
        };
        run.RaiseDomainEvent(new DiscoveryRunCreatedDomainEvent(run.Id, run.DeviceId));
        return run;
    }

    public void Complete()
    {
        EnsureNotFinalized();

        Status = DiscoveryRunStatus.Completed;
        FinishedAt = AppTimeProvider.Current.GetUtcNow().UtcDateTime;
        RaiseDomainEvent(new DiscoveryRunUpdatedDomainEvent(Id, DeviceId, Status));
    }

    public void Fail(string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("A mensagem de erro é obrigatória.", nameof(errorMessage));

        errorMessage = errorMessage.Trim();
        if (errorMessage.Length > ErrorMessageMaxLength)
            throw new ArgumentException(
                $"A mensagem de erro pode ter no máximo {ErrorMessageMaxLength} caracteres.",
                nameof(errorMessage));

        EnsureNotFinalized();

        Status = DiscoveryRunStatus.Failed;
        FinishedAt = AppTimeProvider.Current.GetUtcNow().UtcDateTime;
        ErrorMessage = errorMessage;

        RaiseDomainEvent(new DiscoveryRunUpdatedDomainEvent(Id, DeviceId, Status));
    }

    private void EnsureNotFinalized()
    {
        if (Status is DiscoveryRunStatus.Completed or DiscoveryRunStatus.Failed)
            throw new InvalidOperationException("A execução de discovery já foi finalizada.");
    }
}
