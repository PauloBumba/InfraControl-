using InfraControl.Domain.Common;
using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.RemoteAccess;

/// <summary>
/// Credencial de acesso remoto por device (F11-T006, ADR-013 / docs/10):
/// o domínio só conhece a senha já cifrada — o texto puro nunca entra na
/// entidade (ICredentialProtector cuida da cifra na camada Application).
/// </summary>
public sealed class DeviceCredential : Entity
{
    public const int UsernameMaxLength = 256;
    public const int ProtectedPasswordMaxLength = 1024;

    private DeviceCredential()
    {
    }

    public Guid DeviceId { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public string ProtectedPassword { get; private set; } = string.Empty;
    public DateTime? RotatedAt { get; private set; }

    public static DeviceCredential Set(
        Guid deviceId, string username, string protectedPassword)
    {
        if (deviceId == Guid.Empty)
            throw new ArgumentException("Device é obrigatório.", nameof(deviceId));

        Validate(username, protectedPassword);

        return new DeviceCredential
        {
            DeviceId = deviceId,
            Username = username.Trim(),
            ProtectedPassword = protectedPassword
        };
    }

    public void Rotate(string username, string protectedPassword)
    {
        Validate(username, protectedPassword);

        Username = username.Trim();
        ProtectedPassword = protectedPassword;
        RotatedAt = AppTimeProvider.Current.GetUtcNow().UtcDateTime;
        Touch();
    }

    private static void Validate(string username, string protectedPassword)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("O usuário é obrigatório.", nameof(username));

        if (username.Trim().Length > UsernameMaxLength)
            throw new ArgumentException(
                $"O usuário deve ter no máximo {UsernameMaxLength} caracteres.",
                nameof(username));

        if (string.IsNullOrWhiteSpace(protectedPassword))
            throw new ArgumentException("A senha é obrigatória.", nameof(protectedPassword));

        if (protectedPassword.Length > ProtectedPasswordMaxLength)
            throw new ArgumentException(
                $"A senha deve ter no máximo {ProtectedPasswordMaxLength} caracteres.",
                nameof(protectedPassword));
    }
}
