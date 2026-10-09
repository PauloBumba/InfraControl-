namespace InfraControl.Application.AI;

/// <summary>
/// Registro de providers conhecidos pelo gateway (ADR-037
/// <c>IProviderRegistry</c>): v1 por configuração/env (S4), evolução para
/// banco + UI Admin sem trocar os consumidores.
/// </summary>
public interface IAiProviderRegistry
{
    /// <summary>
    /// Devolve o provider pelo nome; ausente =
    /// <see cref="AiProviderNotConfiguredException"/>.
    /// </summary>
    IAiProvider Get(string providerName);
}
