using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.RemoteAccess.Credentials.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.RemoteAccess;
using MediatR;
using InfraControl.Domain.Enums;

namespace InfraControl.Application.RemoteAccess.Credentials;

/// <summary>
/// Cadastra ou rotaciona a credencial de acesso remoto do device
/// (F11-T006): a senha é cifrada antes de persistir e a substituição
/// registra <c>RotatedAt</c> (audit no pipeline cobre quem/when).
/// </summary>
public sealed record UpsertDeviceCredentialCommand(
    Guid DeviceId, string Username, string Password)
    : BaseCommand<DeviceCredentialDto>, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;

    public string AuditResource => "DeviceCredential";
}

public sealed class UpsertDeviceCredentialCommandHandler(
    IDeviceRepository deviceRepository,
    IDeviceCredentialRepository credentialRepository,
    ICredentialProtector protector)
    : IRequestHandler<UpsertDeviceCredentialCommand, DeviceCredentialDto>
{
    public async Task<DeviceCredentialDto> Handle(
        UpsertDeviceCredentialCommand request, CancellationToken cancellationToken)
    {
        _ = await deviceRepository.GetByIdAsync(request.DeviceId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Device '{request.DeviceId}' não encontrado.");

        // Validações pt-BR do domínio acontecem após a cifra; inputs vazios
        // são barrados aqui para não depender do formato do store.
        if (string.IsNullOrWhiteSpace(request.Username))
            throw new ArgumentException("O usuário é obrigatório.", nameof(request.Username));

        if (string.IsNullOrWhiteSpace(request.Password))
            throw new ArgumentException("A senha é obrigatória.", nameof(request.Password));

        var protectedPassword = protector.Protect(request.Password);
        var existing = await credentialRepository.GetByDeviceIdAsync(
            request.DeviceId, cancellationToken);

        DeviceCredential credential;
        if (existing is null)
        {
            credential = DeviceCredential.Set(
                request.DeviceId, request.Username, protectedPassword);
            await credentialRepository.AddAsync(credential, cancellationToken);
        }
        else
        {
            credential = existing;
            credential.Rotate(request.Username, protectedPassword);
        }

        await credentialRepository.SaveChangesAsync(cancellationToken);
        return new DeviceCredentialDto(
            credential.DeviceId,
            credential.Username,
            true,
            credential.RotatedAt,
            credential.UpdateDateTime);
    }
}

/// <summary>
/// Remove a credencial do device (F11-T006) — o acesso volta ao fallback
/// global documentado em docs/10.
/// </summary>
public sealed record DeleteDeviceCredentialCommand(Guid DeviceId)
    : BaseCommand, IAuthorizedRequest, IAuditableRequest
{
    public IReadOnlyCollection<string> RequiredRoles =>
        RoleAccess.Administer;

    public string AuditResource => "DeviceCredential";
}

public sealed class DeleteDeviceCredentialCommandHandler(
    IDeviceRepository deviceRepository,
    IDeviceCredentialRepository credentialRepository)
    : IRequestHandler<DeleteDeviceCredentialCommand>
{
    public async Task Handle(
        DeleteDeviceCredentialCommand request, CancellationToken cancellationToken)
    {
        _ = await deviceRepository.GetByIdAsync(request.DeviceId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Device '{request.DeviceId}' não encontrado.");

        var credential = await credentialRepository.GetByDeviceIdAsync(
            request.DeviceId, cancellationToken)
            ?? throw new KeyNotFoundException(
                $"Credencial de acesso remoto do device '{request.DeviceId}' não encontrada.");

        credentialRepository.Remove(credential);
        await credentialRepository.SaveChangesAsync(cancellationToken);
    }
}
