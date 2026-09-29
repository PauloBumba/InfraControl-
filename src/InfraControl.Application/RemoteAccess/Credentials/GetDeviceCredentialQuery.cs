using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.RemoteAccess.Credentials.Ports;
using InfraControl.BuildingBlocks.Application;
using MediatR;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.RemoteAccess.Credentials;

/// <summary>
/// Estado da credencial de acesso remoto do device (F11-T006) — username e
/// flags apenas; a senha nunca sai do store (docs/10).
/// </summary>
public sealed record DeviceCredentialDto(
    Guid DeviceId,
    string Username,
    bool HasCredential,
    DateTime? RotatedAt,
    DateTime UpdatedAt);

/// <summary>
/// Lê a credencial cadastrada do device (F11-T006). Sem credencial no
/// store, devolve <c>HasCredential = false</c> — o acesso cai no fallback
/// global (RemoteAccess:Ssh) documentado em docs/10.
/// </summary>
public sealed record GetDeviceCredentialQuery(Guid DeviceId)
    : IQuery<DeviceCredentialDto>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;

    public string AuditResource => "DeviceCredential";
}

public sealed class GetDeviceCredentialQueryHandler(
    IDeviceRepository deviceRepository,
    IDeviceCredentialRepository credentialRepository)
    : IRequestHandler<GetDeviceCredentialQuery, DeviceCredentialDto>
{
    public async Task<DeviceCredentialDto> Handle(
        GetDeviceCredentialQuery request, CancellationToken cancellationToken)
    {
        _ = await deviceRepository.GetByIdAsync(request.DeviceId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Device '{request.DeviceId}' não encontrado.");

        var credential = await credentialRepository.GetByDeviceIdAsync(
            request.DeviceId, cancellationToken);

        return credential is null
            ? new DeviceCredentialDto(request.DeviceId, string.Empty, false, null, default)
            : new DeviceCredentialDto(
                credential.DeviceId,
                credential.Username,
                true,
                credential.RotatedAt,
                credential.UpdateDateTime);
    }
}
