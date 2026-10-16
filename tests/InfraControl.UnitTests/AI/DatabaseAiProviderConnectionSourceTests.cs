using System.Security.Cryptography;
using FluentAssertions;
using InfraControl.Application.RemoteAccess.Credentials.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.AI;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.AI;

/// <summary>
/// ADR-057 (FAI-T011a): conexão efetiva vem do cadastro; chave cifrada
/// é descifrada em memória (e só nela); linha ausente = null (D3-A).
/// </summary>
public sealed class DatabaseAiProviderConnectionSourceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public DatabaseAiProviderConnectionSourceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    private sealed class StubProtector(bool throwOnUnprotect = false) : ICredentialProtector
    {
        public int UnprotectCalls { get; private set; }

        public string Protect(string plainText) => $"v1:{plainText}";

        public string Unprotect(string protectedText)
        {
            UnprotectCalls++;
            if (throwOnUnprotect)
            {
                throw new CryptographicException("chave trocada");
            }

            return protectedText.StartsWith("v1:", StringComparison.Ordinal)
                ? protectedText["v1:".Length..]
                : protectedText;
        }
    }

    [Fact]
    public async Task Provider_Ausente_Devolve_Null()
    {
        await using var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
        var source = new DatabaseAiProviderConnectionSource(
            new AiProviderRepository(context), new StubProtector());

        (await source.FindConnectionAsync("ollama")).Should().BeNull();
    }

    [Fact]
    public async Task Provider_Cadastrado_Devolve_Conexao_E_Descifra_Chave()
    {
        await using var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
        var provider = AiProvider.Create(
            "openai-compatible", "https://api.example", "v1:sk-secreta", ["gpt-4o-mini"]);
        context.AiProviders.Add(provider);
        await context.SaveChangesAsync();

        var protector = new StubProtector();
        var source = new DatabaseAiProviderConnectionSource(
            new AiProviderRepository(context), protector);

        var connection = await source.FindConnectionAsync("openai-compatible");

        connection.Should().NotBeNull();
        connection!.BaseUrl.Should().Be("https://api.example");
        connection.ApiKeyPlain.Should().Be("sk-secreta");
        connection.Models.Should().Equal("gpt-4o-mini");
        connection.IsActive.Should().BeTrue();
        protector.UnprotectCalls.Should().Be(1);
    }

    [Fact]
    public async Task Chave_Cifrada_Irrecuperavel_Degrada_Sem_Credencial()
    {
        await using var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
        var provider = AiProvider.Create(
            "gemini", "https://aistudio.example", "v1:xxx");
        context.AiProviders.Add(provider);
        await context.SaveChangesAsync();

        var source = new DatabaseAiProviderConnectionSource(
            new AiProviderRepository(context), new StubProtector(throwOnUnprotect: true));

        var connection = await source.FindConnectionAsync("gemini");

        connection.Should().NotBeNull();
        connection!.ApiKeyPlain.Should().BeNull();
    }

    [Fact]
    public async Task Linha_Inativa_Continua_Vindo_Com_Flag_De_Inativa()
    {
        await using var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
        var provider = AiProvider.Create("ollama", "http://localhost:11434");
        provider.Deactivate();
        context.AiProviders.Add(provider);
        await context.SaveChangesAsync();

        var source = new DatabaseAiProviderConnectionSource(
            new AiProviderRepository(context), new StubProtector());

        var connection = await source.FindConnectionAsync("ollama");

        connection.Should().NotBeNull();
        connection!.IsActive.Should().BeFalse();
    }
}
