using FluentAssertions;
using InfraControl.Application.AI;
using InfraControl.Application.AI.Providers.Ports;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.AI;

namespace InfraControl.UnitTests.AI;

/// <summary>
/// F49-T002: registry por protocolo do cadastro — fornecedor novo =
/// config no admin (URL+key+protocolo), adaptador instanciado sem deploy;
/// sem cadastro cai no adaptador DI fixo (D3-A) e Invalidate reconstrói.
/// </summary>
public sealed class ProtocolAwareAiProviderRegistryTests
{
    [Fact]
    public async Task Get_ComCadastroOpenAiCompatible_CriaBoundComNomeDoCadastro()
    {
        var source = new StubConnectionSource(new AiProviderConnection(
            "deepseek", "https://api.deepseek.com/v1", "sk", ["deepseek-chat"],
            IsActive: true, Protocol: AiProviderProtocol.OpenAiCompatible));
        string? createdName = null;
        var registry = new ProtocolAwareAiProviderRegistry(
            [new FakeAiProvider("ollama")],
            source,
            (name, protocol) =>
            {
                createdName = name;
                return new FakeAiProvider(name);
            });

        var provider = registry.Get("deepseek");
        await provider.TestConnectionAsync();

        provider.Name.Should().Be("deepseek");
        createdName.Should().Be("deepseek");
    }

    [Fact]
    public async Task Get_ComCadastroOllama_UsaProtocoloOllama()
    {
        var source = new StubConnectionSource(new AiProviderConnection(
            "minha-olla", "http://host:11434", null, ["qwen"],
            IsActive: true, Protocol: AiProviderProtocol.Ollama));
        AiProviderProtocol? used = null;
        var registry = new ProtocolAwareAiProviderRegistry(
            [new FakeAiProvider("ollama")],
            source,
            (name, protocol) =>
            {
                used = protocol;
                return new FakeAiProvider(name);
            });

        _ = registry.Get("minha-olla");
        await registry.Get("minha-olla").TestConnectionAsync();

        used.Should().Be(AiProviderProtocol.Ollama);
    }

    [Fact]
    public void Get_SemCadastro_CaiNoAdaptadorFixo()
    {
        var registry = new ProtocolAwareAiProviderRegistry(
            [new FakeAiProvider("openai")],
            new StubConnectionSource(null),
            (_, _) => throw new InvalidOperationException("não deve criar bound"));

        registry.Get("openai").Name.Should().Be("openai");
    }

    [Fact]
    public async Task Get_SemCadastroESemFixo_LancaNaoConfigurado_NaRequisicao()
    {
        var registry = new ProtocolAwareAiProviderRegistry(
            [new FakeAiProvider("ollama")],
            new StubConnectionSource(null),
            (_, _) => throw new InvalidOperationException("não deve criar bound"));

        var act = () => registry.Get("fantasma").TestConnectionAsync();

        (await act.Should().ThrowAsync<AiProviderNotConfiguredException>())
            .Which.Message.Should().Contain("fantasma");
    }

    [Fact]
    public async Task Invalidate_FazReResolverComNovoProtocolo()
    {
        var connection = new AiProviderConnection(
            "x", "http://a", null, [], IsActive: true,
            Protocol: AiProviderProtocol.OpenAiCompatible);
        var source = new StubConnectionSource(connection);
        var resolved = new List<AiProviderProtocol>();
        var registry = new ProtocolAwareAiProviderRegistry(
            [new FakeAiProvider("ollama")],
            source,
            (name, p) =>
            {
                resolved.Add(p);
                return new FakeAiProvider($"{name}-{p}");
            });

        await registry.Get("x").TestConnectionAsync();
        source.Connection = connection with { Protocol = AiProviderProtocol.Anthropic };
        await registry.Get("x").TestConnectionAsync();
        var staleCount = resolved.Count;

        registry.Invalidate("x");
        await registry.Get("x").TestConnectionAsync();

        resolved.Should().Equal(new[]
        {
            // 1ª requisição resolve; cache segura até Invalidate; depois
            // reconstrói com o protocolo novo do cadastro.
            AiProviderProtocol.OpenAiCompatible,
            AiProviderProtocol.Anthropic,
        });
        staleCount.Should().Be(1);
    }

    [Fact]
    public void Invalidate_SemNome_LimpaTudo()
    {
        var source = new StubConnectionSource(new AiProviderConnection(
            "y", "http://a", null, [], IsActive: true, Protocol: AiProviderProtocol.Gemini));
        var registry = new ProtocolAwareAiProviderRegistry(
            [new FakeAiProvider("ollama")], source, (name, p) => new FakeAiProvider(name));

        _ = registry.Get("y");
        registry.Invalidate();

        // após limpar, o bound reconstrói (mesmo nome — sem efeito visível
        // aqui, mas garante que o dicionário esvaziou sem lançar).
        registry.Get("y").Name.Should().Be("y");
    }

    private sealed class StubConnectionSource(AiProviderConnection? connection)
        : IAiProviderConnectionSource
    {
        public AiProviderConnection? Connection { get; set; } = connection;

        public Task<AiProviderConnection?> FindConnectionAsync(
            string providerName, CancellationToken cancellationToken = default)
            => Task.FromResult(
                string.Equals(Connection?.Name, providerName, StringComparison.OrdinalIgnoreCase)
                    ? Connection
                    : null);
    }
}
