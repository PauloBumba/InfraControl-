namespace InfraControl.Application.AI;

/// <summary>Resposta bruta de um provider (texto + uso de tokens, §43/§55).</summary>
public sealed record AiProviderResult(
    string Text,
    long? PromptTokens = null,
    long? CompletionTokens = null);

/// <summary>Conclusão devolvida ao chamador pelo gateway.</summary>
public sealed record AiCompletion(
    string Text,
    string ProviderName,
    string Model,
    long? PromptTokens,
    long? CompletionTokens,
    bool UsedFallback);

/// <summary>
/// Entrada de auditoria de 1 request de IA (ADR-036/040 · §41/§43):
/// capacidade, provider/modelo usados, se caiu para o fallback, latência,
/// tokens, sucesso/erro e traceId. Persistência = S6.
/// </summary>
public sealed record AiAuditEntry(
    AiCapability Capability,
    string? ProviderName,
    string? Model,
    bool Success,
    bool UsedFallback,
    TimeSpan Latency,
    long? PromptTokens,
    long? CompletionTokens,
    string? Error,
    string? TraceId,
    DateTimeOffset TimestampUtc);
