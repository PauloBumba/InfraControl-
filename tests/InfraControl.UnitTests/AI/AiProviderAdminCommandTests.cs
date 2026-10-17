using FluentAssertions;
using InfraControl.Application.AI;
using InfraControl.Application.AI.Providers;
using InfraControl.Application.AI.Providers.Commands;
using InfraControl.Application.AI.Providers.Ports;
using InfraControl.Application.AI.Providers.Queries;
using InfraControl.Application.RemoteAccess.Credentials.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.AI;

/// <summary>
/// FAI-T011b (ADR-057): CRUD Administer do cadastro de providers —
/// criar/editar (campo vazio preserva a chave), ativar/desativar, rota
/// por capacidade (upsert/delete), teste de conexão (§13), listagem de
/// modelos (§14) e busca com chave mascarada. Harness Sqlite in-memory
/// (molde de AiProviderRepositoryTests).
/// </summary>
public sealed class AiProviderAdminCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public AiProviderAdminCommandTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    private async Task<(AppDbContext Context, AiProviderRepository Repo)> CreateAsync()
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
        return (context, new AiProviderRepository(context));
    }

    private static FakeAdapter Adapter(
        string name, bool ok = true, IReadOnlyList<string>? models = null, bool throws = false)
        => new(name, ok, models ?? ["llama3.2"], throws);

    private sealed class FakeAdapter(
        string name, bool ok, IReadOnlyList<string> models, bool throws) : IAiProvider
    {
        public string Name => name;

        public Task<AiProviderResult> CompleteAsync(
            AiCompletionRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
            => throws ? throw new InvalidOperationException("boom") : Task.FromResult(ok);

        public Task<IReadOnlyList<string>> ListModelsAsync(
            CancellationToken cancellationToken = default)
            => throws
                ? throw new InvalidOperationException("boom")
                : Task.FromResult(models);
    }

    private sealed class StubProtector(bool throwOnUnprotect = false) : ICredentialProtector
    {
        public string Protect(string plainText) => $"enc({plainText})";

        public string Unprotect(string protectedText)
            => throwOnUnprotect
                ? throw new InvalidOperationException("chave corrompida")
                : protectedText["enc(".Length..^1];
    }

    [Fact]
    public async Task Create_Deve_Registrar_Provider_Cifrado_E_Dto_Mascarado()
    {
        var (context, repo) = await CreateAsync();
        var handler = new CreateAiProviderCommandHandler(
            repo, [Adapter("ollama")], new StubProtector());

        var dto = await handler.Handle(
            new CreateAiProviderCommand(
                "Ollama", "http://localhost:11434", "sk-secret", ["llama3.2", "qwen"]),
            CancellationToken.None);

        await repo.SaveChangesAsync();

        dto.Name.Should().Be("ollama");
        dto.Active.Should().BeTrue();
        dto.ApiKeySet.Should().BeTrue();
        dto.ApiKeyMasked.Should().Be("••••cret");
        dto.Models.Should().Equal("llama3.2", "qwen");

        var row = (await repo.GetByNameAsync("ollama"))!;
        row.ApiKeyEncrypted.Should().Be("enc(sk-secret)");
    }

    [Fact]
    public async Task Create_Sem_Adaptador_Registrado_Deve_Falhar_PtBr_Com_Disponiveis()
    {
        var (context, repo) = await CreateAsync();
        var handler = new CreateAiProviderCommandHandler(
            repo, [Adapter("ollama"), Adapter("gemini")], new StubProtector());

        var act = () => handler.Handle(
            new CreateAiProviderCommand("grok", "https://api.grok.dev", null, null),
            CancellationToken.None);

        (await act.Should().ThrowAsync<ArgumentException>())
            .Which.Message.Should().Contain("ollama").And.Contain("gemini");
    }

    [Fact]
    public async Task Create_Nome_Duplicado_Deve_Falhar()
    {
        var (context, repo) = await CreateAsync();
        var handler = new CreateAiProviderCommandHandler(
            repo, [Adapter("ollama")], new StubProtector());
        await handler.Handle(
            new CreateAiProviderCommand("ollama", "http://a", null, null), default);
        await repo.SaveChangesAsync();

        var act = () => handler.Handle(
            new CreateAiProviderCommand("ollama", "http://b", null, null), default);

        (await act.Should().ThrowAsync<ArgumentException>())
            .Which.Message.Should().Contain("já está cadastrado");
    }

    [Fact]
    public async Task Update_Com_Chave_Vazia_Deve_Preservar_E_Com_Chave_Nova_Deve_Trocar()
    {
        var (context, repo) = await CreateAsync();
        var create = new CreateAiProviderCommandHandler(
            repo, [Adapter("ollama")], new StubProtector());
        var created = await create.Handle(
            new CreateAiProviderCommand("ollama", "http://old", "sk-old", ["m1"]), default);
        await repo.SaveChangesAsync();

        var update = new UpdateAiProviderCommandHandler(repo, new StubProtector());
        var semChave = await update.Handle(
            new UpdateAiProviderCommand(created.Id, "http://new", ApiKey: null, ["m2"]),
            default);

        semChave.BaseUrl.Should().Be("http://new");
        semChave.ApiKeySet.Should().BeTrue();
        semChave.Models.Should().Equal("m2");
        (await repo.GetByIdAsync(created.Id))!.ApiKeyEncrypted.Should().Be("enc(sk-old)");

        var comChave = await update.Handle(
            new UpdateAiProviderCommand(created.Id, "http://new", ApiKey: "sk-nova", ["m2"]),
            default);

        comChave.ApiKeyMasked.Should().Be("••••nova");
        (await repo.GetByIdAsync(created.Id))!.ApiKeyEncrypted.Should().Be("enc(sk-nova)");
    }

    [Fact]
    public async Task Update_Provider_Inexistente_Deve_Falhar()
    {
        var (context, repo) = await CreateAsync();
        var handler = new UpdateAiProviderCommandHandler(repo, new StubProtector());

        var act = () => handler.Handle(
            new UpdateAiProviderCommand(Guid.NewGuid(), "http://x", null, null), default);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task SetActive_Deve_Desativar_E_Rativar()
    {
        var (context, repo) = await CreateAsync();
        var create = new CreateAiProviderCommandHandler(
            repo, [Adapter("ollama")], new StubProtector());
        var created = await create.Handle(
            new CreateAiProviderCommand("ollama", "http://a", null, null), default);
        await repo.SaveChangesAsync();

        var handler = new SetAiProviderActiveCommandHandler(repo);
        (await handler.Handle(
            new SetAiProviderActiveCommand(created.Id, Active: false), default)).Active
            .Should().BeFalse();
        await repo.SaveChangesAsync();
        (await repo.GetByIdAsync(created.Id))!.IsActive.Should().BeFalse();

        (await handler.Handle(
            new SetAiProviderActiveCommand(created.Id, Active: true), default)).Active
            .Should().BeTrue();
    }

    [Fact]
    public async Task Upsert_Rota_De_Capacidade_Deve_Criar_E_Atualizar()
    {
        var (context, repo) = await CreateAsync();
        var primary = AiProvider.Create("ollama", "http://a");
        var fallback = AiProvider.Create("gemini", "http://b");
        await repo.AddAsync(primary);
        await repo.AddAsync(fallback);
        await repo.SaveChangesAsync();

        var handler = new UpsertAiProviderCapabilityRouteCommandHandler(repo);
        var dto = await handler.Handle(
            new UpsertAiProviderCapabilityRouteCommand(
                "copilot", primary.Id, "llama3.2", fallback.Id, "gemini-pro"),
            default);

        dto.Capability.Should().Be("Copilot");
        dto.ProviderName.Should().Be("ollama");
        dto.FallbackProviderName.Should().Be("gemini");

        await handler.Handle(
            new UpsertAiProviderCapabilityRouteCommand(
                "COPILOT", fallback.Id, "gemini-pro"),
            default);
        await repo.SaveChangesAsync();

        var route = await repo.GetCapabilityRouteAsync("Copilot");
        route!.ProviderId.Should().Be(fallback.Id);
        route.FallbackProviderId.Should().BeNull();
    }

    [Fact]
    public async Task Upsert_Rota_Com_Capacidade_Invalida_Ou_Fallback_Invalido_Deve_Falhar()
    {
        var (context, repo) = await CreateAsync();
        var primary = AiProvider.Create("ollama", "http://a");
        await repo.AddAsync(primary);
        await repo.SaveChangesAsync();

        var handler = new UpsertAiProviderCapabilityRouteCommandHandler(repo);

        var actCap = () => handler.Handle(
            new UpsertAiProviderCapabilityRouteCommand("qualquer", primary.Id, "m"), default);
        (await actCap.Should().ThrowAsync<ArgumentException>())
            .Which.Message.Should().Contain("Capacidade");

        var actFallback = () => handler.Handle(
            new UpsertAiProviderCapabilityRouteCommand(
                "copilot", primary.Id, "m", Guid.NewGuid(), "fm"), default);
        await actFallback.Should().ThrowAsync<KeyNotFoundException>();

        var actSelf = () => handler.Handle(
            new UpsertAiProviderCapabilityRouteCommand(
                "copilot", primary.Id, "m", primary.Id, "fm"), default);
        (await actSelf.Should().ThrowAsync<ArgumentException>())
            .Which.Message.Should().Contain("fallback");
    }

    [Fact]
    public async Task Delete_Rota_Deve_Remover_Linha()
    {
        var (context, repo) = await CreateAsync();
        var primary = AiProvider.Create("ollama", "http://a");
        await repo.AddAsync(primary);
        await repo.SaveChangesAsync();
        await new UpsertAiProviderCapabilityRouteCommandHandler(repo).Handle(
            new UpsertAiProviderCapabilityRouteCommand("reporting", primary.Id, "m"), default);
        await repo.SaveChangesAsync();

        await new DeleteAiProviderCapabilityRouteCommandHandler(repo).Handle(
            new DeleteAiProviderCapabilityRouteCommand("reporting"), default);
        await repo.SaveChangesAsync();

        (await repo.GetCapabilityRouteAsync("Reporting")).Should().BeNull();
    }

    [Fact]
    public async Task Search_Deve_Listar_Providers_Mascarados_Adaptadores_E_Capacidades()
    {
        var (context, repo) = await CreateAsync();
        var create = new CreateAiProviderCommandHandler(
            repo, [Adapter("ollama"), Adapter("gemini")], new StubProtector());
        var ollama = await create.Handle(
            new CreateAiProviderCommand("ollama", "http://a", "sk-123456", null), default);
        await create.Handle(
            new CreateAiProviderCommand("gemini", "http://b", null, null), default);
        await repo.SaveChangesAsync();
        await new UpsertAiProviderCapabilityRouteCommandHandler(repo).Handle(
            new UpsertAiProviderCapabilityRouteCommand("copilot", ollama.Id, "llama3.2"), default);
        await repo.SaveChangesAsync();

        var dto = await new SearchAiProvidersQueryHandler(
            repo, [Adapter("ollama"), Adapter("gemini")], new StubProtector())
            .Handle(new SearchAiProvidersQuery(), default);

        dto.Providers.Should().HaveCount(2);
        dto.Providers.Single(p => p.Name == "ollama").ApiKeyMasked.Should().Be("••••3456");
        dto.Providers.Single(p => p.Name == "gemini").ApiKeySet.Should().BeFalse();
        dto.AvailableAdapterNames.Should().Equal("gemini", "ollama");

        var copilot = dto.Capabilities.Single(c => c.Capability == "Copilot");
        copilot.ProviderName.Should().Be("ollama");
        var knowledge = dto.Capabilities.Single(c => c.Capability == "KnowledgeGeneration");
        knowledge.ProviderId.Should().BeNull();
        knowledge.ProviderName.Should().BeNull();
    }

    [Fact]
    public async Task Search_Com_Unprotect_Quebrado_Deve_Mascarar_Sem_Sufixo()
    {
        var (context, repo) = await CreateAsync();
        var row = AiProvider.Create("ollama", "http://a", apiKeyEncrypted: "enc(x)");
        await repo.AddAsync(row);
        await repo.SaveChangesAsync();

        var dto = await new SearchAiProvidersQueryHandler(
            repo, [Adapter("ollama")], new StubProtector(throwOnUnprotect: true))
            .Handle(new SearchAiProvidersQuery(), default);

        var ollama = dto.Providers.Single();
        ollama.ApiKeySet.Should().BeTrue();
        ollama.ApiKeyMasked.Should().Be("••••••");
    }

    [Fact]
    public async Task TestConnection_Deve_Cobrir_Ok_Falha_E_Sem_Adaptador()
    {
        var (context, repo) = await CreateAsync();
        var create = new CreateAiProviderCommandHandler(
            repo, [Adapter("ollama"), Adapter("gemini")], new StubProtector());
        var ok = await create.Handle(
            new CreateAiProviderCommand("ollama", "http://a", null, null), default);
        var quebra = await create.Handle(
            new CreateAiProviderCommand("gemini", "http://b", null, null), default);
        await repo.AddAsync(AiProvider.Create("legado", "http://c"));
        await repo.SaveChangesAsync();
        var legado = (await repo.GetByNameAsync("legado"))!;

        var handler = new TestAiProviderConnectionCommandHandler(
            repo, [Adapter("ollama"), Adapter("gemini", throws: true)]);

        (await handler.Handle(new TestAiProviderConnectionCommand(ok.Id), default))
            .Should().Be(new AiProviderConnectionTestDto(Ok: true, Error: null));
        var falha = await handler.Handle(
            new TestAiProviderConnectionCommand(quebra.Id), default);
        falha.Ok.Should().BeFalse();
        falha.Error.Should().Contain("boom");
        var semAdaptador = await handler.Handle(
            new TestAiProviderConnectionCommand(legado.Id), default);
        semAdaptador.Ok.Should().BeFalse();
        semAdaptador.Error.Should().Contain("legado");
    }

    [Fact]
    public async Task ListModels_Deve_Devolver_Modelos_E_Erro_PtBr()
    {
        var (context, repo) = await CreateAsync();
        var create = new CreateAiProviderCommandHandler(
            repo, [Adapter("ollama", models: ["a", "b"])], new StubProtector());
        var ok = await create.Handle(
            new CreateAiProviderCommand("ollama", "http://a", null, null), default);
        await repo.AddAsync(AiProvider.Create("legado", "http://c"));
        await repo.SaveChangesAsync();
        var legado = (await repo.GetByNameAsync("legado"))!;

        var handler = new ListAiProviderModelsCommandHandler(
            repo, [Adapter("ollama", models: ["a", "b"])]);

        (await handler.Handle(new ListAiProviderModelsCommand(ok.Id), default)).Models
            .Should().Equal("a", "b");
        var semAdaptador = await handler.Handle(
            new ListAiProviderModelsCommand(legado.Id), default);
        semAdaptador.Models.Should().BeEmpty();
        semAdaptador.Error.Should().Contain("legado");
    }

    [Fact]
    public void Todos_Os_Slices_Declaram_Administer()
    {
        InfraControl.BuildingBlocks.Application.IAuthorizedRequest[] requests =
        [
            new SearchAiProvidersQuery(),
            new CreateAiProviderCommand("x", "http://x", null, null),
            new UpdateAiProviderCommand(Guid.NewGuid(), "http://x", null, null),
            new SetAiProviderActiveCommand(Guid.NewGuid(), true),
            new UpsertAiProviderCapabilityRouteCommand("copilot", Guid.NewGuid(), "m"),
            new DeleteAiProviderCapabilityRouteCommand("copilot"),
            new TestAiProviderConnectionCommand(Guid.NewGuid()),
            new ListAiProviderModelsCommand(Guid.NewGuid()),
        ];

        requests.Should().OnlyContain(
            request => request.RequiredRoles.SequenceEqual(
                InfraControl.Domain.Enums.RoleAccess.Administer));
    }
}
