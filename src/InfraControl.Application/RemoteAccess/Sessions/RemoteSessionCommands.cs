using InfraControl.Application.RemoteAccess.Sessions.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using InfraControl.Domain.RemoteAccess;
using MediatR;

namespace InfraControl.Application.RemoteAccess.Sessions;

/// <summary>
/// Mapeamento compartilhado dos commands de sessão de acesso remoto
/// (SSH F11-T002, RDP F11-T003).
/// </summary>
internal static class RemoteSessionFlow
{
    internal static string Truncate(string reason)
    {
        var trimmed = reason.Trim();
        return trimmed.Length <= RemoteSession.FailureReasonMaxLength
            ? trimmed
            : trimmed[..RemoteSession.FailureReasonMaxLength];
    }

    internal static RemoteSessionDto ToDto(RemoteSession session) =>
        new(
            session.Id,
            session.UserId,
            session.Protocol.ToString(),
            session.Status.ToString(),
            session.FailureReason,
            session.StartedAt,
            session.EndedAt);
}

/// <summary>
/// Encerra a sessão de acesso remoto (SSH ou RDP) e descarta a conexão
/// registrada (F11-T003: comando é protocolo-agnóstico — um único DELETE).
/// </summary>
public sealed record CloseRemoteSessionCommand(Guid DeviceId, Guid SessionId)
    : BaseCommand<RemoteSessionDto>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;

    public string AuditResource => "RemoteSession";
}

public sealed class CloseRemoteSessionCommandHandler(
    IRemoteSessionRepository sessionRepository,
    IRemoteConnectionRegistry connections)
    : IRequestHandler<CloseRemoteSessionCommand, RemoteSessionDto>
{
    public async Task<RemoteSessionDto> Handle(
        CloseRemoteSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await sessionRepository.GetByIdAsync(request.SessionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Sessão '{request.SessionId}' não encontrada.");

        if (session.DeviceId != request.DeviceId)
            throw new KeyNotFoundException($"Sessão '{request.SessionId}' não encontrada.");

        session.Close();
        connections.TryDisconnect(session.Id);
        await sessionRepository.SaveChangesAsync(cancellationToken);
        return RemoteSessionFlow.ToDto(session);
    }
}
