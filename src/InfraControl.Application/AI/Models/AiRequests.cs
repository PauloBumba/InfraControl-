namespace InfraControl.Application.AI;

/// <summary>Opções de geração repassadas ao provider (§55: limites configuráveis).</summary>
public sealed record AiGenerationOptions(
    int? MaxTokens = null,
    double? Temperature = null);

/// <summary>
/// Pedido de conclusão criado pelo chamador. O <b>modelo não é escolha
/// de quem chama</b>: o gateway preenche <c>Model</c> a partir do router
/// por capacidade (ADR-037 · §15) antes de entregar ao provider.
/// <c>Actor</c>/<c>TraceId</c> são metadados de rastreabilidade (§41/§43)
/// repassados à auditoria — o chamador informa quem perguntou.
/// </summary>
public sealed record AiCompletionRequest(
    AiCapability Capability,
    string Prompt,
    string? SystemPrompt = null,
    AiGenerationOptions? Generation = null,
    string? TraceId = null,
    string? Model = null,
    string? Actor = null);
