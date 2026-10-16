using InfraControl.Domain.Common.Entities;

namespace InfraControl.Domain.Entities;

/// <summary>
/// ADR-057 (FAI-T011a): roteamento capacidade → provider + modelo no
/// cadastro (espelha <c>AiRoutingModelOptions</c>, ADR-037 §15/§54).
/// Uma linha por capacidade (Copilot/IncidentAnalysis/KnowledgeGeneration/
/// Reporting/Automation — nome do enum <c>AiCapability</c>, case-
/// insensitive). Sem linha, a capacidade cai no fallback de config
/// (D3-A) e, sem nada, no estado "não configurado" (§44).
/// </summary>
public sealed class AiProviderCapability : Entity
{
    public const int MaxCapabilityLength = 64;
    public const int MaxModelLength = 128;

    public string Capability { get; private set; } = string.Empty;
    public Guid ProviderId { get; private set; }
    public string DefaultModel { get; private set; } = string.Empty;
    public Guid? FallbackProviderId { get; private set; }
    public string? FallbackModel { get; private set; }

    private AiProviderCapability() { }

    public static AiProviderCapability Create(
        string capability,
        Guid providerId,
        string defaultModel,
        Guid? fallbackProviderId = null,
        string? fallbackModel = null)
    {
        var route = new AiProviderCapability();
        route.Update(capability, providerId, defaultModel, fallbackProviderId, fallbackModel);
        return route;
    }

    public void Update(
        string capability,
        Guid providerId,
        string defaultModel,
        Guid? fallbackProviderId,
        string? fallbackModel)
    {
        var trimmedCapability = capability?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedCapability))
            throw new ArgumentException("Capacidade é obrigatória.", nameof(capability));
        if (trimmedCapability.Length > MaxCapabilityLength)
            throw new ArgumentException(
                $"Capacidade pode ter no máximo {MaxCapabilityLength} caracteres.",
                nameof(capability));
        if (providerId == Guid.Empty)
            throw new ArgumentException("Provider da capacidade é obrigatório.", nameof(providerId));

        var trimmedDefaultModel = defaultModel?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedDefaultModel))
            throw new ArgumentException("Modelo default da capacidade é obrigatório.", nameof(defaultModel));
        if (trimmedDefaultModel.Length > MaxModelLength)
            throw new ArgumentException(
                $"Modelo pode ter no máximo {MaxModelLength} caracteres.", nameof(defaultModel));

        var trimmedFallbackModel = string.IsNullOrWhiteSpace(fallbackModel)
            ? null
            : fallbackModel.Trim();
        if (trimmedFallbackModel is { Length: > MaxModelLength })
            throw new ArgumentException(
                $"Modelo de fallback pode ter no máximo {MaxModelLength} caracteres.",
                nameof(fallbackModel));
        if (trimmedFallbackModel is not null && fallbackProviderId is null)
            throw new ArgumentException(
                "FallbackModel exige FallbackProviderId.", nameof(fallbackModel));

        Capability = trimmedCapability;
        ProviderId = providerId;
        DefaultModel = trimmedDefaultModel;
        FallbackProviderId = fallbackProviderId;
        FallbackModel = trimmedFallbackModel;
        Touch();
    }
}
