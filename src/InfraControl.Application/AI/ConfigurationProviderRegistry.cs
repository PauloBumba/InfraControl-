namespace InfraControl.Application.AI;

/// <summary>
/// Registro v1 de providers por configuração/env (ADR-037 §2 · amenda
/// ADR-050 §1): recebe todos os <c>IAiProvider</c> registrados na DI e
/// resolve pelo <c>Name</c> (case-insensitive). Evolução futura:
/// <c>DatabaseProviderRegistry</c> com tabelas + UI Admin.
/// </summary>
public sealed class ConfigurationProviderRegistry : IAiProviderRegistry
{
    private readonly Dictionary<string, IAiProvider> _providers;

    public ConfigurationProviderRegistry(IEnumerable<IAiProvider> providers)
    {
        _providers = providers.ToDictionary(
            provider => provider.Name,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Devolve o provider pelo nome. Nome ausente do registro =
    /// <see cref="AiProviderNotConfiguredException"/> (§44).
    /// </summary>
    public IAiProvider Get(string providerName)
    {
        if (!string.IsNullOrWhiteSpace(providerName)
            && _providers.TryGetValue(providerName, out var provider))
        {
            return provider;
        }

        throw new AiProviderNotConfiguredException(
            $"Provider '{providerName}' não está configurado.");
    }
}
