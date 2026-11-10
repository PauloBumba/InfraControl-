using System.Collections.Concurrent;
using InfraControl.Application.AI;
using InfraControl.Application.AI.Providers.Ports;
using InfraControl.Domain.Enums;
using Microsoft.Extensions.Options;

namespace InfraControl.Infrastructure.AI;

/// <summary>
/// F49-T002: registry por PROTOCOLO do cadastro — o operador cadastra
/// qualquer fornecedor (URL + key + protocolo) e o registry instancia o
/// adaptador certo sem deploy (DeepSeek/Mistral/Groq = openai-compatible).
/// Resolve: cache bound → cadastro (protocolo) → adaptador DI fixo (D3-A
/// sem cadastro) → §44. A instância bound só carrega o NOME; a conexão
/// (URL/key fresca) é lida pelo adaptador a cada requisição (D3 intacto).
/// </summary>
public sealed class ProtocolAwareAiProviderRegistry : IAiProviderRegistry, IAiProviderRegistryCache
{
    private readonly ConcurrentDictionary<string, IAiProvider> _bound =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConfigurationProviderRegistry _fixed;
    private readonly IAiProviderConnectionSource _connectionSource;
    private readonly Func<string, AiProviderProtocol, IAiProvider> _adapterFactory;

    public ProtocolAwareAiProviderRegistry(
        IEnumerable<IAiProvider> fixedAdapters,
        IHttpClientFactory httpClientFactory,
        IAiProviderConnectionSource connectionSource,
        IOptions<AiOpenAiCompatibleOptions> openAiOptions,
        IOptions<AiGeminiOptions> geminiOptions,
        IOptions<AiOllamaOptions> ollamaOptions,
        IOptions<AiAnthropicOptions> anthropicOptions)
        : this(
            fixedAdapters,
            connectionSource,
            CreateAdapterFactory(
                httpClientFactory, connectionSource,
                openAiOptions, geminiOptions, ollamaOptions, anthropicOptions))
    {
    }

    /// <summary>Ctor enxuto p/ testes (factory direto, sem HTTP).</summary>
    public ProtocolAwareAiProviderRegistry(
        IEnumerable<IAiProvider> fixedAdapters,
        IAiProviderConnectionSource connectionSource,
        Func<string, AiProviderProtocol, IAiProvider> adapterFactory)
    {
        _fixed = new ConfigurationProviderRegistry(fixedAdapters);
        _connectionSource = connectionSource;
        _adapterFactory = adapterFactory;
    }

    public IAiProvider Get(string providerName)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            throw new AiProviderNotConfiguredException("Provider não informado.");

        return _bound.GetOrAdd(
            providerName.Trim(),
            name => new LazyBoundProvider(name, ResolveAsync, _fixed));
    }

    public void Invalidate(string? providerName = null)
    {
        if (providerName is null)
            _bound.Clear();
        else
            _bound.TryRemove(providerName.Trim(), out _);
    }

    private async Task<IAiProvider> ResolveAsync(string name, CancellationToken cancellationToken)
    {
        var connection = await _connectionSource.FindConnectionAsync(name, cancellationToken);
        if (connection is null)
        {
            // Sem cadastro: cai no adaptador DI fixo (D3-A) — que lança
            // §44 se o nome também não estiver em config.
            return _fixed.Get(name);
        }

        return _adapterFactory(name, connection.Protocol);
    }

    private static Func<string, AiProviderProtocol, IAiProvider> CreateAdapterFactory(
        IHttpClientFactory httpClientFactory,
        IAiProviderConnectionSource connectionSource,
        IOptions<AiOpenAiCompatibleOptions> openAiOptions,
        IOptions<AiGeminiOptions> geminiOptions,
        IOptions<AiOllamaOptions> ollamaOptions,
        IOptions<AiAnthropicOptions> anthropicOptions)
        => (name, protocol) => protocol switch
        {
            AiProviderProtocol.Ollama => new OllamaProvider(
                httpClientFactory, ollamaOptions, connectionSource, name),
            AiProviderProtocol.Gemini => new GeminiProvider(
                httpClientFactory,
                Options.Create(new AiGeminiOptions
                {
                    Name = name,
                    BaseUrl = geminiOptions.Value.BaseUrl,
                    ApiKey = null,
                    Models = [],
                }),
                connectionSource),
            AiProviderProtocol.Anthropic => new AnthropicProvider(
                httpClientFactory,
                Options.Create(new AiAnthropicOptions
                {
                    Name = name,
                    BaseUrl = anthropicOptions.Value.BaseUrl,
                    ApiKey = null,
                    Models = [],
                }),
                connectionSource),
            // openai-compatible é o default universal
            // (DeepSeek/Mistral/Groq/Together…).
            _ => new OpenAiCompatibleProvider(
                httpClientFactory,
                Options.Create(new AiOpenAiCompatibleOptions
                {
                    Name = name,
                    BaseUrl = openAiOptions.Value.BaseUrl,
                    ApiKey = null,
                    Models = [],
                }),
                connectionSource),
        };
}

/// <summary>
/// Wrapper lazy: o adaptador certo só é instanciado na primeira
/// requisição (lê o protocolo do cadastro) e fica cacheado; a conexão
/// fresca continua sendo lida pelo próprio adaptador (D3).
/// </summary>
internal sealed class LazyBoundProvider(
    string name,
    Func<string, CancellationToken, Task<IAiProvider>> resolve,
    ConfigurationProviderRegistry fallback) : IAiProvider
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IAiProvider? _inner;

    public string Name => name;

    public Task<AiProviderResult> CompleteAsync(
        AiCompletionRequest request, CancellationToken cancellationToken = default)
        => WithInnerAsync((inner, ct) => inner.CompleteAsync(request, ct), cancellationToken);

    public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        => WithInnerAsync((inner, ct) => inner.TestConnectionAsync(ct), cancellationToken);

    public Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken = default)
        => WithInnerAsync((inner, ct) => inner.ListModelsAsync(ct), cancellationToken);

    private async Task<T> WithInnerAsync<T>(
        Func<IAiProvider, CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        if (_inner is { } cached)
            return await action(cached, cancellationToken);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_inner is null)
            {
                try
                {
                    _inner = await resolve(name, cancellationToken);
                }
                catch (AiProviderNotConfiguredException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new AiProviderNotConfiguredException(
                        $"Provider '{name}' não pôde ser resolvido: {ex.Message}");
                }
            }

            return await action(_inner, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }
}
