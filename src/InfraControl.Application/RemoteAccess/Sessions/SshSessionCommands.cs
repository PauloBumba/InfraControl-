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
    : BaseCommand<RemoteSessionDto>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.RemoteSshConnect.ToString()];

    public string AuditResource => "RemoteSession";
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
            var reason = RemoteSessionFlow.Truncate(
                result.Error ?? "Falha desconhecida ao conectar via SSH.");
            session.Fail(reason);
            await sessionRepository.AddAsync(session, cancellationToken);
            await sessionRepository.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Falha ao abrir sessão SSH em '{device.Name}': {reason}");
        }

        connections.Register(session.Id, result.Connection);
        await sessionRepository.AddAsync(session, cancellationToken);
        await sessionRepository.SaveChangesAsync(cancellationToken);
        return RemoteSessionFlow.ToDto(session);
    }
}
