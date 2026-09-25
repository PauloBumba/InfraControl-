namespace InfraControl.Infrastructure.RemoteAccess;

/// <summary>
/// Chave-símétrica da credencial de acesso remoto (F11-T006) — valor vem
/// só de env/config (`RemoteAccess:CredentialEncryption:Key`, base64 de 32
/// bytes); nunca versionado no git.
/// </summary>
public sealed class CredentialEncryptionOptions
{
    public const string SectionName = "RemoteAccess:CredentialEncryption";

    public string Key { get; set; } = string.Empty;
}
