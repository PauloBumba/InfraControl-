using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using InfraControl.Application.AI;
using InfraControl.Application.AI.Providers.Commands;
using InfraControl.Application.AI.Providers.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Enums;
using InfraControl.IntegrationTests.Common;
using InfraControl.Infrastructure.AI;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.IntegrationTests.AI;

/// <summary>
/// FAI-T011c (ADR-057): smoke ao vivo da resolução cadastro-primeiro.
/// PostgreSQL real (Testcontainers) + migração/schema reais + seeder,
/// commands e router/gateway/adaptadores reais — só a rede é um stub
/// local (HttpListener em127.0.0.1:18765, porta fixa do AiOllamaOptions
/// do harness). Prova o caminho compartilhado pelos 7 endpoints de IA
/// (/ai/chat · incidents/{id}/investigate · incidents/{id}/generate-
/// knowledge · tasks/{taskId}/generate-knowledge/ai · automation/
/// recommendations · reports/transversal/ai-summary · runtime health):
/// todos resolvem capacidade via MESMO router.
/// </summary>
public sealed class AiProviderCadastroResolveTests : BaseIntegrationTest
{
    private const string StubBaseUrl = "http://127.0.0.1:18765";
    private readonly StubAiServer _stub = new();

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _stub.Start();

        CurrentUser.Roles = ["Admin"];

        using var scope = CreateScope();
        await scope.ServiceProvider.GetRequiredService<AiProviderSeeder>().SeedAsync();
    }

    public override async Task DisposeAsync()
    {
        await _stub.DisposeAsync();
        await base.DisposeAsync();
    }

    private async Task<T> SendAsync<T>(IRequest<T> request)
    {
        using var scope = CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request);
    }

    private async Task SendAsync(ICommand request)
    {
        using var scope = CreateScope();
        await scope.ServiceProvider.GetRequiredService<IMediator>().Send(request);
    }

    [Fact]
    public async Task Seed_Cria_Ollama_Com_Url_Do_Config_E_E_Idempotente()
    {
        using var scope = CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAiProviderRepository>();

        var ollama = await repo.GetByNameAsync("ollama");
        ollama.Should().NotBeNull();
        ollama!.BaseUrl.Should().Be(StubBaseUrl);

        await scope.ServiceProvider.GetRequiredService<AiProviderSeeder>().SeedAsync();
        (await repo.ListAsync()).Count(provider => provider.Name == "ollama").Should().Be(1);
    }

    [Fact]
    public async Task Cadastro_De_Segundo_Provider_Via_Commands_Domina_Router_E_Gateway()
    {
        var openai = await SendAsync(new CreateAiProviderCommand(
            "openai", StubBaseUrl, "cadastro-smoke-key", ["gpt-4o-mini"]));
        openai.ApiKeySet.Should().BeTrue();

        // rota do Copilot + das outras4 capacidades → openai (cadastro)
        foreach (var capability in Enum.GetNames<AiCapability>())
        {
            var route = await SendAsync(new UpsertAiProviderCapabilityRouteCommand(
                capability, openai.Id, "gpt-4o-mini"));
            route.ProviderName.Should().Be("openai");
        }

        using (var scope = CreateScope())
        {
            var router = scope.ServiceProvider.GetRequiredService<IAiProviderRouter>();
            foreach (var capability in Enum.GetNames<AiCapability>())
            {
                var selection = await router.ResolveAsync(
                    Enum.Parse<AiCapability>(capability));
                selection.Provider.Should().Be("openai");
                selection.Model.Should().Be("gpt-4o-mini");
            }
        }

        // gateway ao vivo: a request SAI com a chave e a URL do CADASTRO
        using (var scope = CreateScope())
        {
            var gateway = scope.ServiceProvider.GetRequiredService<IAiGateway>();
            var completion = await gateway.CompleteAsync(new AiCompletionRequest(
                AiCapability.Copilot, "ping do smoke T011c"));

            completion.ProviderName.Should().Be("openai");
            completion.Text.Should().Be("resposta-do-stub-openai");
        }

        var lastOpenAi = _stub.LastRequest("/chat/completions");
        lastOpenAi.Should().NotBeNull();
        lastOpenAi!.Authorization.Should().Be("Bearer cadastro-smoke-key");
    }

    [Fact]
    public async Task Sem_Rota_De_Capacidade_Cai_No_Fallback_De_Config()
    {
        // garante cadastro + rota p/ depois remover (ordem via IDisposable do xUnit
        // não garante ordem entre facts — cada fact monta o próprio cenário)
        var openai = await SendAsync(new CreateAiProviderCommand(
            "openai", StubBaseUrl, "cadastro-smoke-key", ["gpt-4o-mini"]));
        await SendAsync(new UpsertAiProviderCapabilityRouteCommand(
            "Copilot", openai.Id, "gpt-4o-mini"));
        await SendAsync(new DeleteAiProviderCapabilityRouteCommand("Copilot"));

        using var scope = CreateScope();
        var router = scope.ServiceProvider.GetRequiredService<IAiProviderRouter>();
        var selection = await router.ResolveAsync(AiCapability.Copilot);
        selection.Provider.Should().Be("ollama", "sem linha de rota a resolução cai no config (D3-A)");

        var gateway = scope.ServiceProvider.GetRequiredService<IAiGateway>();
        var completion = await gateway.CompleteAsync(new AiCompletionRequest(
            AiCapability.Copilot, "ping fallback config"));

        completion.ProviderName.Should().Be("ollama");
        completion.Text.Should().Be("resposta-do-stub-ollama");

        var lastOllama = _stub.LastRequest("/api/generate");
        lastOllama.Should().NotBeNull();
        lastOllama!.Authorization.Should().BeNull(
            "a conexão vem do config (sem linha no cadastro) — sem chave de cadastro");
    }

    [Fact]
    public async Task Provider_Inativo_Nunca_Burla_Config_E_Vira_Secao_44()
    {
        using (var scope = CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IAiProviderRepository>();
            var ollama = (await repo.GetByNameAsync("ollama"))!;
            await SendAsync(new SetAiProviderActiveCommand(ollama.Id, Active: false));
        }

        using var scope2 = CreateScope();
        var router = scope2.ServiceProvider.GetRequiredService<IAiProviderRouter>();
        var act = () => router.ResolveAsync(AiCapability.Copilot);

        (await act.Should().ThrowAsync<AiProviderNotConfiguredException>())
            .Which.Message.Should().Contain("inativo");

        var gateway = scope2.ServiceProvider.GetRequiredService<IAiGateway>();
        var actGateway = () => gateway.CompleteAsync(new AiCompletionRequest(
            AiCapability.Copilot, "ping inativo"));
        await actGateway.Should().ThrowAsync<AiProviderNotConfiguredException>();
    }

    /// <summary>Stub HTTP local: responde no formato de cada adaptador e registra
    /// o último request por caminho (evidência de BaseUrl/ApiKey do cadastro).</summary>
    private sealed class StubAiServer : IAsyncDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly object _gate = new();
        private readonly Dictionary<string, StubRequest> _last = new();
        private Task? _loop;

        public void Start()
        {
            _listener.Prefixes.Add("http://127.0.0.1:18765/");
            _listener.Start();
            _loop = Task.Run(AcceptLoopAsync);
        }

        public StubRequest? LastRequest(string pathPart)
        {
            lock (_gate)
            {
                return _last.TryGetValue(pathPart, out var request) ? request : null;
            }
        }

        private async Task AcceptLoopAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (Exception)
                {
                    return;
                }

                _ = Task.Run(async () =>
                {
                    var path = context.Request.Url?.AbsolutePath ?? "";
                    lock (_gate)
                    {
                        var key = _last.Keys.FirstOrDefault(item => path.Contains(item))
                            ?? path;
                        _last[key] = new StubRequest(
                            path,
                            context.Request.Headers["Authorization"]);
                    }

                    var body = path.Contains("/chat/completions")
                        ? """{"choices":[{"message":{"role":"assistant","content":"resposta-do-stub-openai"}}]}"""
                        : """{"model":"llama3.2","response":"resposta-do-stub-ollama","done":true}""";
                    var bytes = Encoding.UTF8.GetBytes(body);
                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "application/json";
                    await context.Response.OutputStream.WriteAsync(bytes);
                    context.Response.Close();
                });
            }
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            try
            {
                _listener.Stop();
                _listener.Close();
            }
            catch (Exception)
            {
                // listener já parado
            }

            if (_loop is not null)
            {
                try
                {
                    await _loop;
                }
                catch (Exception)
                {
                    // loop encerrado pelo Stop
                }
            }

            _cts.Dispose();
        }
    }

    private sealed record StubRequest(string Path, string? Authorization);
}
