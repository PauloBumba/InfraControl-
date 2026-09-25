using InfraControl.Domain.Common;
using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.RemoteAccess;

/// <summary>
/// Sessão de acesso remoto registrada (F11-T001, ADR-013 / docs/10):
/// quem conectou, em qual device, quando e com qual desfecho. A entidade é
/// o registro auditável da sessão — credenciais nunca fazem parte dela.
/// </summary>
public sealed class RemoteSession : Entity
{
    public const int FailureReasonMaxLength = 500;

    private RemoteSession()
    {
    }

    public Guid DeviceId { get; private set; }
    public Guid UserId { get; private set; }
    public RemoteProtocol Protocol { get; private set; }
    public RemoteSessionStatus Status { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? EndedAt { get; private set; }

    public static RemoteSession Open(Guid deviceId, Guid userId, RemoteProtocol protocol)
    {
        if (deviceId == Guid.Empty)
            throw new ArgumentException("Device é obrigatório.", nameof(deviceId));

        if (userId == Guid.Empty)
            throw new ArgumentException("Usuário é obrigatório.", nameof(userId));

        if (!Enum.IsDefined(protocol))
            throw new ArgumentOutOfRangeException(
                nameof(protocol), protocol, "Protocolo de acesso remoto inválido.");

        return new RemoteSession
        {
            DeviceId = deviceId,
            UserId = userId,
            Protocol = protocol,
            Status = RemoteSessionStatus.Active,
            StartedAt = AppTimeProvider.Current.GetUtcNow().UtcDateTime
        };
    }

    public void Close()
    {
        EnsureActive();
        Status = RemoteSessionStatus.Closed;
        EndedAt = AppTimeProvider.Current.GetUtcNow().UtcDateTime;
    }

    public void Fail(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("O motivo da falha é obrigatório.", nameof(reason));

        if (reason.Trim().Length > FailureReasonMaxLength)
            throw new ArgumentException(
                $"O motivo da falha deve ter no máximo {FailureReasonMaxLength} caracteres.",
                nameof(reason));

        EnsureActive();
        Status = RemoteSessionStatus.Failed;
        FailureReason = reason.Trim();
        EndedAt = AppTimeProvider.Current.GetUtcNow().UtcDateTime;
    }

    private void EnsureActive()
    {
        if (Status != RemoteSessionStatus.Active)
            throw new InvalidOperationException("A sessão já foi finalizada.");
    }
}
