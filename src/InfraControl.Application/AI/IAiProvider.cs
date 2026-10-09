namespace InfraControl.Application.AI;

/// <summary>
/// Contrato de provedor de LLM (ADR-037): implementações (ex.:
/// <c>OllamaProvider</c>, S4) vivem em <c>Infrastructure/AI</c> e são o
/// ÚNICO ponto de rede para a IA (docs/34 §9–§10 — nada de SDK do
/// domínio espalhado). Só é alcançável pelo gateway.
/// </summary>
public interface IAiProvider
{
    /// <summary>Identificador estável do provider (ex.: <c>ollama</c>).</summary>
    string Name { get; }

    Task<AiProviderResult> CompleteAsync(
        AiCompletionRequest request,
        CancellationToken cancellationToken = default);
}
