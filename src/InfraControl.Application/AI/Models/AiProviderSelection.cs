namespace InfraControl.Application.AI;

/// <summary>
/// Resultado do router por capacidade: provider/modelo primários +
/// fallback opcional (ADR-037 · docs/34 §15/§54).
/// </summary>
public sealed record AiProviderSelection(
    string Provider,
    string Model,
    string? FallbackProvider = null,
    string? FallbackModel = null);
