using FluentAssertions;
using InfraControl.Application.AI;
using InfraControl.Application.AI.Tools;
using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InfraControl.UnitTests.AI;

/// <summary>
/// FAI-T008/S7 (docs/34 §41/§52 · ADR-040): executor de tools —
/// registry (estado "não configurado" §44), autorização pela MESMA
/// hierarquia RoleAccess do pipeline MediatR (autenticação sempre;
/// overlap OR de roles), timeout por tool, auditoria
/// <c>AI.Tool.{nome}</c> (Succeeded/Denied/Failed com ator e trace) e
/// teto de resultados. Sem Docker: fakes.
/// </summary>
public sealed class AiToolExecutorTests
{
    private sealed class FakeCurrentUser(bool authenticated, params string[] roles) : ICurrentUserContext
    {
        public Guid? UserId { get; } = authenticated ? Guid.NewGuid() : null;
        public string? UserName { get; } = authenticated ? "tester" : null;
        public bool IsAuthenticated { get; } = authenticated;
        public IReadOnlyCollection<string> Roles { get; } = roles;
    }

    private sealed class FakeTool(AiToolDescriptor descriptor) : IAiTool
    {
        public AiToolDescriptor Descriptor { get; } = descriptor;
        public Func<AiToolArguments, Task<AiToolResult>>? Handler { get; set; }
        public TimeSpan? Delay { get; set; }

        public async Task<AiToolResult> ExecuteAsync(
            AiToolArguments arguments,
            CancellationToken cancellationToken = default)
        {
            if (Delay is not null)
                await Task.Delay(Delay.Value, cancellationToken);

            return Handler is null
                ? new AiToolResult(Descriptor.Name, [], 0, Descriptor.Source, DateTimeOffset.UtcNow)
                : await Handler(arguments);
        }
    }

    private sealed class FakeAuditWriter : IAuditWriter
    {
        public List<AuditEntry> Entries { get; } = [];

        public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public void Dispose() { }
    }

    private static AiToolDescriptor Descriptor(
        AiToolAccess access = AiToolAccess.Read,
        TimeSpan? timeout = null,
        int maxResults = 10,
        string name = "ferramenta") =>
        new(
            name,
            "tool de teste",
            access,
            timeout ?? TimeSpan.FromSeconds(5),
            maxResults,
            """{"arg?": "string"}""",
            """{"findings[]": []}""",
            "AI.Tool.*",
            "Teste/FakeQuery");

    private static (AiToolExecutor Executor, FakeAuditWriter Audit) Build(
        params IAiTool[] tools)
    {
        var audit = new FakeAuditWriter();
        var executor = new AiToolExecutor(
            new InMemoryToolRegistry(tools),
            new FakeCurrentUser(authenticated: true, "Viewer"),
            audit,
            new FakeUnitOfWork(),
            NullLogger<AiToolExecutor>.Instance);
        return (executor, audit);
    }

    [Fact]
    public async Task ExecuteAsync_tool_inexistente_lanca_nao_configurada_44()
    {
        var (executor, _) = Build(new FakeTool(Descriptor()));

        var act = async () => await executor.ExecuteAsync("nao-existe", AiToolArguments.Empty);

        var failure = await act.Should().ThrowAsync<AiToolNotFoundException>();
        failure.Which.Message.Should().Contain("não está registrada");
    }

    [Fact]
    public async Task ExecuteAsync_sem_autenticacao_e_negado_e_auditado()
    {
        var audit = new FakeAuditWriter();
        var executor = new AiToolExecutor(
            new InMemoryToolRegistry([new FakeTool(Descriptor())]),
            new FakeCurrentUser(authenticated: false),
            audit,
            new FakeUnitOfWork(),
            NullLogger<AiToolExecutor>.Instance);

        var act = async () => await executor.ExecuteAsync("ferramenta", AiToolArguments.Empty);

        await act.Should().ThrowAsync<AiToolAuthorizationException>();
        var entry = audit.Entries.Should().ContainSingle().Which;
        entry.Action.Should().Be("AI.Tool.ferramenta");
        entry.Outcome.Should().Be("Denied");
        entry.Error.Should().Contain("Autenticação");
    }

    [Fact]
    public async Task ExecuteAsync_role_incompativel_e_negado_com_roles_ausentes()
    {
        var audit = new FakeAuditWriter();
        var executor = new AiToolExecutor(
            new InMemoryToolRegistry([new FakeTool(Descriptor(AiToolAccess.Administer))]),
            new FakeCurrentUser(authenticated: true, "Viewer"),
            audit,
            new FakeUnitOfWork(),
            NullLogger<AiToolExecutor>.Instance);

        var act = async () => await executor.ExecuteAsync("ferramenta", AiToolArguments.Empty);

        var failure = await act.Should().ThrowAsync<AiToolAuthorizationException>();
        failure.Which.Message.Should().Contain("Roles ausentes");
        audit.Entries.Should().ContainSingle(entry => entry.Outcome == "Denied");
    }

    [Fact]
    public async Task ExecuteAsync_operator_passa_em_tool_operate()
    {
        var tool = new FakeTool(Descriptor(AiToolAccess.Operate));
        var executor = new AiToolExecutor(
            new InMemoryToolRegistry([tool]),
            new FakeCurrentUser(authenticated: true, "Operator"),
            new FakeAuditWriter(),
            new FakeUnitOfWork(),
            NullLogger<AiToolExecutor>.Instance);

        var result = await executor.ExecuteAsync("ferramenta", AiToolArguments.Empty);

        result.ToolName.Should().Be("ferramenta");
    }

    [Fact]
    public async Task ExecuteAsync_timeout_vira_excecao_ptbr_e_audita_failed()
    {
        var audit = new FakeAuditWriter();
        var slowTool = new FakeTool(Descriptor(timeout: TimeSpan.FromMilliseconds(50)))
        {
            Delay = TimeSpan.FromSeconds(2),
        };
        var executor = new AiToolExecutor(
            new InMemoryToolRegistry([slowTool]),
            new FakeCurrentUser(authenticated: true, "Viewer"),
            audit,
            new FakeUnitOfWork(),
            NullLogger<AiToolExecutor>.Instance);

        var act = async () => await executor.ExecuteAsync("ferramenta", AiToolArguments.Empty);

        var failure = await act.Should().ThrowAsync<AiToolTimeoutException>();
        failure.Which.Message.Should().Contain("excedeu o timeout");
        audit.Entries.Should().ContainSingle(entry => entry.Outcome == "Failed");
    }

    [Fact]
    public async Task ExecuteAsync_sucesso_audita_Succeeded_com_ator_e_trunca_no_MaxResults()
    {
        var manyFindings = Enumerable
            .Range(1, 20)
            .Select(index => new AiToolFinding(index.ToString(), $"achado {index}"))
            .ToList();
        var tool = new FakeTool(Descriptor(maxResults: 5))
        {
            Handler = _ => Task.FromResult(
                new AiToolResult("ferramenta", manyFindings, 20, "Teste/FakeQuery", DateTimeOffset.UtcNow)),
        };
        var audit = new FakeAuditWriter();
        var executor = new AiToolExecutor(
            new InMemoryToolRegistry([tool]),
            new FakeCurrentUser(authenticated: true, "Viewer"),
            audit,
            new FakeUnitOfWork(),
            NullLogger<AiToolExecutor>.Instance);

        var result = await executor.ExecuteAsync("ferramenta", AiToolArguments.Empty);

        result.Findings.Should().HaveCount(5, "o executor aplica o teto do descriptor §52");
        var entry = audit.Entries.Should().ContainSingle().Which;
        entry.Action.Should().Be("AI.Tool.ferramenta");
        entry.Outcome.Should().Be("Succeeded");
        entry.ActorId.Should().Be("tester");
    }

    [Fact]
    public void Registry_lista_e_resolve_case_insensitive()
    {
        var tool = new FakeTool(Descriptor(name: "get_sla_status"));
        var registry = new InMemoryToolRegistry([tool]);

        registry.Get("GET_SLA_STATUS").Should().BeSameAs(tool);
        registry.List().Should().ContainSingle();
    }
}
