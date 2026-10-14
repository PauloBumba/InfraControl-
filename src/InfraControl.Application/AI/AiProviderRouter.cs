namespace InfraControl.Application.AI;

/// <summary>
/// Implementação do <c>IAiProviderRouter</c> (ADR-037 §15): resolve
/// uma capacidade em (provider, modelo) primário + fallback (§54).
/// Sem mapeamento configurado lança
/// <see cref="AiProviderNotConfiguredException"/> (estado "não
/// configurado" degradado, §44).
/// </summary>
public sealed class AiProviderRouter : IAiProviderRouter
{
    private readonly IAiProviderRegistry _providerRegistry;
    private readonly AiRoutingOptionsConfiguration _configuration;

    public AiProviderRouter(
        IAiProviderRegistry providerRegistry,
        AiRoutingOptionsConfiguration configuration)
    {
        _providerRegistry = providerRegistry;
        _configuration = configuration;
    }

    /// <summary>
    /// Resolve a capacidade pedida em (provider, modelo) primário + fallback.
    /// </summary>
    public AiProviderSelection Resolve(AiCapability capability)
    {
        var options = capability switch
        {
            AiCapability.Copilot => _configuration.Copilot,
            AiCapability.IncidentAnalysis => _configuration.IncidentAnalysis,
            AiCapability.KnowledgeGeneration => _configuration.KnowledgeGeneration,
            AiCapability.Reporting => _configuration.Reporting,
            AiCapability.Automation => _configuration.Automation,
            _ => throw new AiProviderNotConfiguredException(
                $"Capacidade '{capability}' não está configurada.")
        };

        if (string.IsNullOrWhiteSpace(options.DefaultModel))
        {
            throw new AiProviderNotConfiguredException(
                $"Modelo default não configurado para a capacidade '{capability}'.");
        }

        // ADR-050 §2: a capacidade escolhe o provider (vazio = "ollama",
        // backward compat com ADR-037 §15). O primário é validado no
        // registry aqui — falha cedo no estado "não configurado" (§44).
        var providerName = string.IsNullOrWhiteSpace(options.Provider)
            ? "ollama"
            : options.Provider.Trim();
        var primaryProvider = _providerRegistry.Get(providerName);

        string? fallbackProvider = null;
        if (!string.IsNullOrWhiteSpace(options.FallbackModel))
        {
            var fallbackProviderName = string.IsNullOrWhiteSpace(options.FallbackProvider)
                ? providerName
                : options.FallbackProvider.Trim();
            fallbackProvider = _providerRegistry.Get(fallbackProviderName).Name;
        }

        return new AiProviderSelection(
            Provider: primaryProvider.Name,
            Model: options.DefaultModel,
            FallbackProvider: fallbackProvider,
            FallbackModel: options.FallbackModel);
    }
}
