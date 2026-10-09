namespace InfraControl.Application.AI;

/// <summary>
/// Registro v1 de providers por configuração/env (ADR-037 §2):
/// lê <c>Ai__*</c> de appsettings/.env e devolve instâncias conhecidas.
/// Evolução futura: <c>DatabaseProviderRegistry</c> com tabelas + UI Admin.
/// </summary>
public sealed class ConfigurationProviderRegistry : IAiProviderRegistry
{
    private readonly IAiProvider _ollamaProvider;

    public ConfigurationProviderRegistry(IAiProvider ollamaProvider)
    {
        _ollamaProvider = ollamaProvider;
    }

    /// <summary>
    /// Devolve o provider pelo nome. Apenas <c>"ollama"</c> é suportado
    /// na v1 por configuração (ADR-037 §2).
    /// </summary>
    public IAiProvider Get(string providerName)
    {
        return providerName?.ToLowerInvariant() switch
        {
            "ollama" => _ollamaProvider,
            _ => throw new AiProviderNotConfiguredException(
                $"Provider '{providerName}' não está configurado.")
        };
    }
}
