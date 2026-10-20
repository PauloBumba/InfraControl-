using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.RemoteAccess.Sessions.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using InfraControl.Domain.RemoteAccess;
using MediatR;

namespace InfraControl.Application.RemoteAccess.Sessions;

/// <summary>
/// Abre uma sessão de acesso remoto pelo GATEWAY (Guacamole, ADR-060 D1/D2,
/// F34-T007): valida device (IP + SO informado) e credencial, minta token
/// SSO via header auth e registra a <see cref="RemoteSession"/> — histórico
/// e encerramento reaproveitam o fluxo F11 (GET/DELETE do
/// RemoteSessionsController). A conexão TCP/WS com o alvo fica por conta
/// do guacd (pilotos F34-T004/T005).
/// </summary>
public sealed record OpenRemoteGatewaySessionCommand(Guid DeviceId)
    : BaseCommand<RemoteGatewaySessionDto>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Operate;

    public string AuditResource => "RemoteSession";
}

/// <summary>
/// Sessão mintada no gateway: o front monta <c>{origin}{path}?token={token}</c>
/// e abre em nova aba — o navegador nunca vê credencial (D4).
/// </summary>
public sealed record RemoteGatewaySessionDto(
    Guid SessionId,
    string Protocol,
    string Token,
    string Path);

public sealed class OpenRemoteGatewaySessionCommandHandler(
    IDeviceRepository deviceRepository,
    IRemoteSessionRepository sessionRepository,
    IRemoteCredentialSource credentialSource,
    IGuacamoleGateway gateway,
    ICurrentUserContext currentUser)
    : IRequestHandler<OpenRemoteGatewaySessionCommand, RemoteGatewaySessionDto>
{
    internal const string GatewayPath = "/guacamole/";

    public async Task<RemoteGatewaySessionDto> Handle(
        OpenRemoteGatewaySessionCommand request, CancellationToken cancellationToken)
    {
        var device = await deviceRepository.GetByIdAsync(request.DeviceId, cancellationToken)
            ?? throw new KeyNotFoundException($"Device '{request.DeviceId}' não encontrado.");

        if (string.IsNullOrWhiteSpace(device.Ip))
            throw new ArgumentException(
                $"Device '{device.Name}' não possui endereço IP para acesso remoto.",
                nameof(request.DeviceId));

        if (device.OperatingSystem is null)
            throw new ArgumentException(
                $"Device '{device.Name}' não possui sistema operacional informado — "
                + "edite a ficha antes de abrir o acesso remoto.",
                nameof(request.DeviceId));

        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException(
                "Autenticação é necessária para esta requisição.");

        var userName = currentUser.UserName
            ?? throw new UnauthorizedAccessException(
                "Autenticação é necessária para esta requisição.");

        // Credencial validada aqui (mensagem pt-BR do fallback em
        // ConfigurationRemoteCredentialSource) — o valor NUNCA sai da memória
        // nem vira DTO (D4); no gateway vira a conexão Guacamole (T008: cria/
        // atualiza conexão + grant do usuário header-auth, server-to-server).
        var credential = await credentialSource.GetCredentialAsync(
            device.Id, cancellationToken);

        var protocol = device.OperatingSystem == DeviceOperatingSystem.Windows
            ? RemoteProtocol.Rdp
            : RemoteProtocol.Ssh;

        await gateway.EnsureDeviceConnectionAsync(
            device.Name, device.Ip!, protocol, credential, userName, cancellationToken);

        var token = await gateway.CreateTokenAsync(userName, cancellationToken);

        var session = RemoteSession.Open(device.Id, userId, protocol);
        await sessionRepository.AddAsync(session, cancellationToken);
        await sessionRepository.SaveChangesAsync(cancellationToken);

        return new RemoteGatewaySessionDto(
            session.Id, protocol.ToString(), token, GatewayPath);
    }
}
