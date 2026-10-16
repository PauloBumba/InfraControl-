using InfraControl.Application.AI.Providers.Ports;
using InfraControl.Application.RemoteAccess.Credentials.Ports;
using InfraControl.Domain.Entities;

namespace InfraControl.Infrastructure.AI;

/// <summary>
/// ADR-057 (FAI-T011a): conexão efetiva do provider = cadastro (D3).
/// Linha ausente = <c>null</c> (o chamador cai na config, D3-A). A chave
/// é descifrada aqui (ICredentialProtector/AES-GCM, F11-T006) e só
/// existe em memória durante a chamada — nunca log/DTO/estado.
/// </summary>
public sealed class DatabaseAiProviderConnectionSource(
    IAiProviderRepository repository,
    ICredentialProtector credentialProtector)
    : IAiProviderConnectionSource
{
    public async Task<AiProviderConnection?> FindConnectionAsync(
        string providerName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return null;
        }

        var provider = await repository.GetByNameAsync(providerName.Trim(), cancellationToken);
        if (provider is null)
        {
            return null;
        }

        return new AiProviderConnection(
            Name: provider.Name,
            BaseUrl: provider.BaseUrl,
            ApiKeyPlain: UnprotectQuietly(provider.ApiKeyEncrypted),
            Models: provider.Models,
            IsActive: provider.IsActive);
    }

    private string? UnprotectQuietly(string? apiKeyEncrypted)
    {
        if (string.IsNullOrWhiteSpace(apiKeyEncrypted))
        {
            return null;
        }

        try
        {
            return credentialProtector.Unprotect(apiKeyEncrypted);
        }
        catch
        {
            // Chave trocada/ausente: degrada sem credencial (§44) — o
            // provider decide se segue sem Bearer ou recusa.
            return null;
        }
    }
}
