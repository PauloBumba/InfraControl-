namespace InfraControl.Application.AI;

/// <summary>
/// Limites do gateway por chamada (ADR-036/040 · docs/34 §55): valores
/// padrão conservadores, sobrescrevíveis por configuração (`Ai:*`) quando
/// o S4 registrar o gateway no host. Espelha o estilo de
/// <see cref="Common.Interfaces.ResilienceOptions"/>.
/// </summary>
public sealed record AiGatewayOptions(
    int MaxRetries = 1,
    TimeSpan? Timeout = null,
    bool FallbackEnabled = true)
{
    /// <summary>Timeout por tentativa (default 30 s — LLM local é lento).</summary>
    public TimeSpan EffectiveTimeout => Timeout ?? TimeSpan.FromSeconds(30);
}
