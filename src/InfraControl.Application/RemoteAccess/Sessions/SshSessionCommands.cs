using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.RemoteAccess.Sessions.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Authorization;
using InfraControl.Domain.RemoteAccess;
using MediatR;

namespace InfraControl.Application.RemoteAccess.Sessions;

/// <summary>Abre uma sessão SSH no device (F11-T002, ADR-013).</summary>
public sealed record OpenSshSessionCommand(Guid DeviceId)
    : BaseCommand<RemoteSessionDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.RemoteSshConnect.ToString()];
}

/// <summary>Encerra a sessão SSH ativa do device e descarta a conexão (F11-T002).</summary>
public sealed record CloseSshSessionCommand(Guid DeviceId, Guid SessionId)
    : BaseCommand<RemoteSessionDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.RemoteSshConnect.ToString()];
}

public sealed class OpenSshSessionCommandHandler(
    IDeviceRepository deviceRepository,
    IRemoteSessionRepository sessionRepository,
    IProviderRegistry<IRemoteProvider> providers,
    IRemoteCredentialSource credentialSource,
    IRemoteConnectionRegistry connections,
    ICurrentUserContext currentUser)
    : IRequestHandler<OpenSshSessionCommand, RemoteSessionDto>
{
    public async Task<RemoteSessionDto> Handle(
        OpenSshSessionCommand request, CancellationToken cancellationToken)
    {
        var device = await deviceRepository.GetByIdAsync(request.DeviceId, cancellationToken)
            ?? throw new KeyNotFoundException($"Device '{request.DeviceId}' não encontrado.");

        if (string.IsNullOrWhiteSpace(device.Ip))
            throw new ArgumentException(
                $"Device '{device.Name}' não possui endereço IP para acesso remoto.",
                nameof(request.DeviceId));

        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException(
                "Autenticação é necessária para esta requisição.");

        var credential = await credentialSource.GetCredentialAsync(device.Id, cancellationToken);
        var provider = providers.Resolve(RemoteProtocol.Ssh.ToString());

        var session = RemoteSession.Open(device.Id, userId, RemoteProtocol.Ssh);
        var result = await provider.ConnectAsync(device.Ip, credential, cancellationToken);

        if (!result.Succeeded || result.Connection is null)
        {
            var reason = Truncate(result.Error ?? "Falha desconhecida ao conectar via SSH.");
            session.Fail(reason);
            await sessionRepository.AddAsync(session, cancellationToken);
            await sessionRepository.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Falha ao abrir sessão SSH em '{device.Name}': {reason}");
        }

        connections.Register(session.Id, result.Connection);
        await sessionRepository.AddAsync(session, cancellationToken);
        await sessionRepository.SaveChangesAsync(cancellationToken);
        return ToDto(session);
    }

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

public sealed class CloseSshSessionCommandHandler(
    IRemoteSessionRepository sessionRepository,
    IRemoteConnectionRegistry connections)
    : IRequestHandler<CloseSshSessionCommand, RemoteSessionDto>
{
    public async Task<RemoteSessionDto> Handle(
        CloseSshSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await sessionRepository.GetByIdAsync(request.SessionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Sessão '{request.SessionId}' não encontrada.");

        if (session.DeviceId != request.DeviceId)
            throw new KeyNotFoundException($"Sessão '{request.SessionId}' não encontrada.");

        session.Close();
        connections.TryDisconnect(session.Id);
        await sessionRepository.SaveChangesAsync(cancellationToken);
        return OpenSshSessionCommandHandler.ToDto(session);
    }
}
