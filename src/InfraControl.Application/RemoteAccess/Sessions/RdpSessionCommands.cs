using InfraControl.Application.Common.Interfaces;
using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.RemoteAccess.Sessions.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.RemoteAccess;
using MediatR;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.RemoteAccess.Sessions;

/// <summary>
/// Abre uma sessão RDP no device via probe TCP/X.224 (F11-T003, ADR-013):
/// confirma que o endpoint fala RDP, sem autenticação — NLA/CredSSP fica
/// para task futura (F11-T006+), então nenhuma credencial é consultada.
/// </summary>
public sealed record OpenRdpSessionCommand(Guid DeviceId)
    : BaseCommand<RemoteSessionDto>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;

    public string AuditResource => "RemoteSession";
}

public sealed class OpenRdpSessionCommandHandler(
    IDeviceRepository deviceRepository,
    IRemoteSessionRepository sessionRepository,
    IProviderRegistry<IRemoteProvider> providers,
    IRemoteConnectionRegistry connections,
    ICurrentUserContext currentUser)
    : IRequestHandler<OpenRdpSessionCommand, RemoteSessionDto>
{
    /// <summary>
    /// O probe RDP não usa credencial — interface exige o parâmetro.
    /// </summary>
    private static readonly RemoteCredential ProbeCredential =
        new(string.Empty, string.Empty);

    public async Task<RemoteSessionDto> Handle(
        OpenRdpSessionCommand request, CancellationToken cancellationToken)
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

        var provider = providers.Resolve(RemoteProtocol.Rdp.ToString());

        var session = RemoteSession.Open(device.Id, userId, RemoteProtocol.Rdp);
        var result = await provider.ConnectAsync(device.Ip, ProbeCredential, cancellationToken);

        if (!result.Succeeded || result.Connection is null)
        {
            var reason = RemoteSessionFlow.Truncate(
                result.Error ?? "Falha desconhecida ao conectar via RDP.");
            session.Fail(reason);
            await sessionRepository.AddAsync(session, cancellationToken);
            await sessionRepository.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Falha ao abrir sessão RDP em '{device.Name}': {reason}");
        }

        connections.Register(session.Id, result.Connection);
        await sessionRepository.AddAsync(session, cancellationToken);
        await sessionRepository.SaveChangesAsync(cancellationToken);
        return RemoteSessionFlow.ToDto(session);
    }
}
