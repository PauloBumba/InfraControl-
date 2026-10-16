using InfraControl.Application.AI;
using InfraControl.Application.AI.Providers.Ports;
using InfraControl.Application.Common.Interfaces;

namespace InfraControl.UnitTests.AI;

/// <summary>
/// Fakes compartilhados dos testes da camada de IA (a partir do
/// FAI-T006/S5 — os testes do S3/S4 têm os seus próprios, privados).
/// </summary>
internal sealed class FakeAiProvider(string name) : IAiProvider
{
    public string Name { get; } = name;
    public List<AiCompletionRequest> Calls { get; } = [];
    public Func<AiCompletionRequest, AiProviderResult>? Handler { get; set; }
    public Exception? Fault { get; set; }
    public bool ConnectionOk { get; set; } = true;
    public Exception? ConnectionFault { get; set; }

    public Task<AiProviderResult> CompleteAsync(
        AiCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        Calls.Add(request);
        if (Fault is not null)
            return Task.FromException<AiProviderResult>(Fault);

        return Task.FromResult(Handler is null
            ? new AiProviderResult($"resposta-{Name}")
            : Handler(request));
    }

    public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default) =>
        ConnectionFault is null
            ? Task.FromResult(ConnectionOk)
            : Task.FromException<bool>(ConnectionFault);
}

/// <summary>Router fixo; selection nula simula capacidade sem mapeamento (§44).</summary>
internal sealed class FakeAiRouter(AiProviderSelection? selection) : IAiProviderRouter
{
    public Task<AiProviderSelection> ResolveAsync(
        AiCapability capability,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(selection
        ?? throw new AiProviderNotConfiguredException(
            $"Capacidade de IA '{capability}' não está configurada (nenhum provider/modelo associado)."));
}

/// <summary>
/// Fonte de conexão fixa (ADR-057 FAI-T011a). <c>Null</c> = sem linha no
/// cadastro (os adaptadores caem na config, D3-A).
/// </summary>
internal sealed class FakeAiConnectionSource(AiProviderConnection? connection)
    : IAiProviderConnectionSource
{
    public static readonly FakeAiConnectionSource Null = new(connection: null);

    public Task<AiProviderConnection?> FindConnectionAsync(
        string providerName,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(connection);
}

internal sealed class FakeAiRegistry(params IAiProvider[] providers) : IAiProviderRegistry
{
    private readonly Dictionary<string, IAiProvider> _providers =
        providers.ToDictionary(provider => provider.Name, StringComparer.OrdinalIgnoreCase);

    public IAiProvider Get(string providerName) =>
        _providers.TryGetValue(providerName, out var provider)
            ? provider
            : throw new AiProviderNotConfiguredException(
                $"Provider de IA '{providerName}' não está registrado.");
}

/// <summary>Executa inline (sem backoff real) e captura policy/opções repassadas.</summary>
internal sealed class FakeResilienceExecutor : IResilienceExecutor
{
    public List<(string Policy, ResilienceOptions? Options)> Calls { get; } = [];

    public Task<T> ExecuteAsync<T>(
        string policyName,
        Func<CancellationToken, Task<T>> operation,
        ResilienceOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Calls.Add((policyName, options));
        cancellationToken.ThrowIfCancellationRequested();
        return operation(cancellationToken);
    }
}

internal sealed class FakeAuditSink : IAiAuditSink
{
    public List<AiAuditEntry> Entries { get; } = [];

    public Task RecordAsync(AiAuditEntry entry, CancellationToken cancellationToken = default)
    {
        Entries.Add(entry);
        return Task.CompletedTask;
    }
}
