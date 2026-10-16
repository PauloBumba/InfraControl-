namespace InfraControl.Application.AI;

/// <summary>
/// Resolve a capacidade pedida em (provider, modelo) primário + fallback
/// (ADR-037 · docs/34 §15/§54). ADR-057 (FAI-T011a): a resolução é
/// primária pelo cadastro (<c>ai_provider_capabilities</c>); sem linha
/// de rota cai no fallback de configuração (D3-A); sem nada, lança
/// <see cref="AiProviderNotConfiguredException"/> (estado "não
/// configurado" degradado, §44 — nunca resposta inventada).
/// </summary>
public interface IAiProviderRouter
{
    Task<AiProviderSelection> ResolveAsync(
        AiCapability capability,
        CancellationToken cancellationToken = default);
}
