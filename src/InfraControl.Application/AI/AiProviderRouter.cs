using InfraControl.Application.AI.Providers.Ports;

namespace InfraControl.Application.AI;

/// <summary>
/// Implementação do <c>IAiProviderRouter</c> (ADR-037 §15): resolve
/// uma capacidade em (provider, modelo) primário + fallback (§54).
/// ADR-057 (FAI-T011a): rota no cadastro primeiro (D3-A) — provider
/// inativo/ausente = "não configurado" (§44); sem linha de rota no
/// cadastro, o mapeamento de configuração (ADR-037 §15) segue como
/// fallback. Adaptador concreto validado no registry (ADR-050 §1).
/// </summary>
public sealed class AiProviderRouter : IAiProviderRouter
{
    private readonly IAiProviderRegistry _providerRegistry;
    private readonly AiRoutingOptionsConfiguration _configuration;
    private readonly IAiProviderRepository _repository;

    public AiProviderRouter(
        IAiProviderRegistry providerRegistry,
        AiRoutingOptionsConfiguration configuration,
        IAiProviderRepository repository)
    {
        _providerRegistry = providerRegistry;
        _configuration = configuration;
        _repository = repository;
    }

    /// <summary>
    /// Resolve a capacidade pedida em (provider, modelo) primário + fallback.
    /// </summary>
    public async Task<AiProviderSelection> ResolveAsync(
        AiCapability capability,
        CancellationToken cancellationToken = default)
    {
        var selection = await ResolveFromRegistryAsync(capability, cancellationToken);
        if (selection is not null)
        {
            return selection;
        }

        return ResolveFromConfiguration(capability);
    }

    /// <summary>
    /// ADR-057: rota do cadastro — linha presente é autoridade; provider
    /// inativo/ausente cai em §44 (nunca em config por baixo, para não
    /// burlar a desativação do operador).
    /// </summary>
    private async Task<AiProviderSelection?> ResolveFromRegistryAsync(
        AiCapability capability,
        CancellationToken cancellationToken)
    {
        var route = await _repository.GetCapabilityRouteAsync(
            capability.ToString(), cancellationToken);
        if (route is null)
        {
            return null;
        }

        var provider = await _repository.GetByIdAsync(route.ProviderId, cancellationToken);
        if (provider is null || !provider.IsActive)
        {
            throw new AiProviderNotConfiguredException(
                $"Capacidade '{capability}' aponta para provider "
                + $"{(provider is null ? "inexistente" : $"'{provider.Name}' (inativo)")} no cadastro.");
        }

        // Validação cedo do adaptador (§44): registra o provider pelo
        // nome do cadastro — erro claro se o adaptador não existe.
        var primaryProvider = _providerRegistry.Get(provider.Name);

        string? fallbackProvider = null;
        string? fallbackModel = null;
        if (route.FallbackProviderId is Guid fallbackProviderId)
        {
            var fallback = await _repository.GetByIdAsync(fallbackProviderId, cancellationToken);
            if (fallback is null || !fallback.IsActive)
            {
                throw new AiProviderNotConfiguredException(
                    $"Fallback da capacidade '{capability}' aponta para provider "
                    + $"{(fallback is null ? "inexistente" : $"'{fallback.Name}' (inativo)")} no cadastro.");
            }

            fallbackProvider = _providerRegistry.Get(fallback.Name).Name;
            fallbackModel = route.FallbackModel;
        }

        return new AiProviderSelection(
            Provider: primaryProvider.Name,
            Model: route.DefaultModel,
            FallbackProvider: fallbackProvider,
            FallbackModel: fallbackModel);
    }

    /// <summary>Fallback ADR-037 §15 (config) — D3-A do ADR-057.</summary>
    private AiProviderSelection ResolveFromConfiguration(AiCapability capability)
    {
        // ADR-065: modelo único (`Routing:Default`) + override opcional por
        // capacidade — a cascatada resolve o par efetivo da capacidade.
        var options = _configuration.EffectiveFor(capability)
            ?? throw new AiProviderNotConfiguredException(
                $"Capacidade '{capability}' não está configurada.");

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
