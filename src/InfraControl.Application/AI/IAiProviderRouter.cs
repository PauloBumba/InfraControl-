namespace InfraControl.Application.AI;

/// <summary>
/// Resolve a capacidade pedida em (provider, modelo) primário + fallback
/// (ADR-037 · docs/34 §15/§54). Implementação de configuração chega no
/// S4 (ConfigurationProviderRegistry); sem mapeamento lança
/// <see cref="AiProviderNotConfiguredException"/> (estado "não
/// configurado" degradado, §44 — nunca resposta inventada).
/// </summary>
public interface IAiProviderRouter
{
    AiProviderSelection Resolve(AiCapability capability);
}
