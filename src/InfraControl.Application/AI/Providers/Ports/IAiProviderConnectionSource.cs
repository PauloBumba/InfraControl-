namespace InfraControl.Application.AI.Providers.Ports;

/// <summary>
/// ADR-057 (FAI-T011a): efetiva conexão de um provider — cadastro
/// primeiro (D3), config de fallback (D3-A). <c>ApiKeyPlain</c> só vive
/// em memória durante a chamada; nunca vira log/DTO/estado.
/// </summary>
public sealed record AiProviderConnection(
    string Name,
    string BaseUrl,
    string? ApiKeyPlain,
    IReadOnlyList<string> Models,
    bool IsActive);

/// <summary>
/// Porta de resolução de conexão usada pelos adaptadores por requisição
/// (o pipeline de chamada já é async — ADR-057 v1, sem cache).
/// </summary>
public interface IAiProviderConnectionSource
{
    Task<AiProviderConnection?> FindConnectionAsync(
        string providerName, CancellationToken cancellationToken = default);
}
