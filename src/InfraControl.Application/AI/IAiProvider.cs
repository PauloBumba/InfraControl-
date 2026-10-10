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

    /// <summary>
    /// Teste de conexão do provider (ADR-037 §13): <c>true</c> quando o
    /// endpoint responde. Usado pelo runtime health da camada de IA
    /// (FAI-T006/S5 · §44). Default otimista para providers sem rede.
    /// </summary>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    /// <summary>
    /// Descoberta de modelos instalados (ADR-037 §14): nomes disponíveis
    /// no provider. Default vazio para providers sem discovery.
    /// </summary>
    Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>([]);
}
