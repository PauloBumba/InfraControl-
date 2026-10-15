using FluentAssertions;
using InfraControl.Application.AI;
using InfraControl.Application.AI.Context;
using InfraControl.Application.AI.Knowledge;
using InfraControl.Application.Incidents;
using InfraControl.Application.Incidents.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace InfraControl.UnitTests.AI;

/// <summary>
/// Fase D (ADR-054): rascunho de knowledge com IA — nasce Draft por
/// domínio, origem K4 (Incident/Task), contrato JSON com fallback e
/// gate de task concluída. Espelha KnowledgeGenerationTests (SQLite
/// in-memory + repositórios reais) com os deps de IA mockados.
/// </summary>
public sealed class AiKnowledgeDraftTests : IDisposable
{
    private static readonly Guid CurrentUserId = Guid.Parse("99999999-1111-2222-3333-444444444444");
    private static readonly Guid IncidentId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly AppDbContext _context;

    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IRetrievalService> _retrieval = new();
    private readonly Mock<IContextBuilder> _contextBuilder = new();
    private readonly Mock<IAiGateway> _gateway = new();

    public AiKnowledgeDraftTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        _context = new AppDbContext(_options);
        _context.Database.EnsureCreatedAsync().GetAwaiter().GetResult();
        _context.DomainUsers.Add(User.Create(
            CurrentUserId, "autor@exemplo.test", "Autor do Teste", UserRole.Operator));
        _context.SaveChangesAsync().GetAwaiter().GetResult();

        _mediator
            .Setup(m => m.Send(It.IsAny<GetIncidentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IncidentDto(
                Id: IncidentId,
                Title: "Link de falha no APP-02",
                Description: "Perda de pacotes desde as 08:00.",
                Severity: "Critical",
                Status: "Investigating",
                DeviceId: null,
                OpenedAt: new DateTime(2026, 10, 13, 8, 0, 0, DateTimeKind.Utc),
                ResolvedAt: null,
                AlertCount: 3,
                AlertIds: []));
        _retrieval
            .Setup(r => r.RetrieveAsync(It.IsAny<RetrievalRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrievalResult([], 0));
        _contextBuilder
            .Setup(c => c.Build(It.IsAny<IReadOnlyList<RetrievalHit>>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(EvidenceSet.Empty);
        _gateway
            .Setup(g => g.CompleteAsync(It.IsAny<AiCompletionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiCompletion(
                """{"title": "Incidente: falha de link no APP-02", "content": "## Contexto\nRede."}""",
                "ollama", "qwen2.5-coder:1.5b", 10, 500, false));
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private sealed class FakeCurrentUser : ICurrentUserContext
    {
        public Guid? UserId { get; } = CurrentUserId;
        public string? UserName => "autor@exemplo.test";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles { get; } = ["Operator"];
    }

    private GenerateIncidentKnowledgeDraftCommandHandler IncidentHandler()
        => new(
            _mediator.Object,
            _retrieval.Object,
            _contextBuilder.Object,
            _gateway.Object,
            new KnowledgeArticleRepository(_context),
            new KnowledgeArticleVersionRepository(_context),
            new KnowledgeArticleOriginRepository(_context),
            new FakeCurrentUser(),
            NullLogger<GenerateIncidentKnowledgeDraftCommandHandler>.Instance);

    private GenerateTaskKnowledgeDraftCommandHandler TaskHandler()
        => new(
            new TaskItemRepository(_context),
            _retrieval.Object,
            _contextBuilder.Object,
            _gateway.Object,
            new KnowledgeArticleRepository(_context),
            new KnowledgeArticleVersionRepository(_context),
            new KnowledgeArticleOriginRepository(_context),
            new FakeCurrentUser(),
            NullLogger<GenerateTaskKnowledgeDraftCommandHandler>.Instance);

    private static async Task<TaskItem> SeedConcludedTaskAsync(AppDbContext context)
    {
        var task = TaskItem.Create("Configurar backup do banco", TaskKind.Manual, "Rotina de backup diário.");
        task.ChangeState(TaskState.InProgress);
        task.ChangeState(TaskState.Resolved);
        context.Tasks.Add(task);
        await context.SaveChangesAsync();
        return task;
    }

    private static AiCompletionRequest CapturedRequest(Mock<IAiGateway> gateway)
        => gateway.Invocations
            .Select(i => i.Arguments[0])
            .OfType<AiCompletionRequest>()
            .Single();

    [Fact]
    public async Task Incident_draft_is_created_as_Draft_with_origin_and_json_contract()
    {
        var response = await IncidentHandler().Handle(
            new GenerateIncidentKnowledgeDraftCommand(IncidentId), CancellationToken.None);

        response.Title.Should().Be("Incidente: falha de link no APP-02");
        response.Content.Should().Be("## Contexto\nRede.");
        response.Status.Should().Be(KnowledgeArticleStatus.Draft);
        response.Version.Should().Be("1.0");

        var originRepository = new KnowledgeArticleOriginRepository(_context);
        (await originRepository.ExistsAsync(
            response.Id, KnowledgeOriginType.Incident, IncidentId)).Should().BeTrue();

        var version = await new KnowledgeArticleVersionRepository(_context)
            .GetLatestAsync(response.Id);
        version.Should().NotBeNull();
        version!.Reason.Should().Contain("IA");
        version.Reason.Should().Contain("revisão humana");
    }

    [Fact]
    public async Task Incident_draft_falls_back_to_source_title_when_response_has_no_json()
    {
        _gateway
            .Setup(g => g.CompleteAsync(It.IsAny<AiCompletionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiCompletion(
                "Resposta sem JSON estrito — texto corrido do modelo.",
                "ollama", "qwen2.5-coder:1.5b", 10, 100, false));

        var response = await IncidentHandler().Handle(
            new GenerateIncidentKnowledgeDraftCommand(IncidentId), CancellationToken.None);

        response.Title.Should().Be("Link de falha no APP-02");
        response.Content.Should().Be("Resposta sem JSON estrito — texto corrido do modelo.");
        response.Status.Should().Be(KnowledgeArticleStatus.Draft);
    }

    [Fact]
    public async Task Incident_draft_clamps_title_to_domain_limit()
    {
        var longTitle = new string('x', 300);
        _gateway
            .Setup(g => g.CompleteAsync(It.IsAny<AiCompletionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AiCompletion(
                $"{{\"title\": \"{longTitle}\", \"content\": \"corpo\"}}",
                "ollama", "qwen2.5-coder:1.5b", 10, 100, false));

        var response = await IncidentHandler().Handle(
            new GenerateIncidentKnowledgeDraftCommand(IncidentId), CancellationToken.None);

        response.Title.Should().HaveLength(200);
    }

    [Fact]
    public async Task Incident_draft_uses_KnowledgeGeneration_with_level0_json_prompt()
    {
        await IncidentHandler().Handle(
            new GenerateIncidentKnowledgeDraftCommand(IncidentId), CancellationToken.None);

        var request = CapturedRequest(_gateway);
        request.Capability.Should().Be(AiCapability.KnowledgeGeneration);
        request.Prompt.Should().Contain("Link de falha no APP-02");
        request.SystemPrompt.Should().Contain("LEVEL 0");
        request.SystemPrompt.Should().Contain("{\"title\"");
        request.SystemPrompt.Should().Contain("NUNCA invente");
    }

    [Fact]
    public async Task Task_draft_requires_concluded_state()
    {
        var task = TaskItem.Create("Em andamento", TaskKind.Manual, null);
        _context.Tasks.Add(task);
        await _context.SaveChangesAsync();

        var act = () => TaskHandler().Handle(
            new GenerateTaskKnowledgeDraftCommand(task.Id), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*concluída*");
    }

    [Fact]
    public async Task Task_draft_is_created_with_task_origin()
    {
        var task = await SeedConcludedTaskAsync(_context);

        var response = await TaskHandler().Handle(
            new GenerateTaskKnowledgeDraftCommand(task.Id), CancellationToken.None);

        response.Status.Should().Be(KnowledgeArticleStatus.Draft);
        response.Title.Should().Be("Incidente: falha de link no APP-02");

        var originRepository = new KnowledgeArticleOriginRepository(_context);
        (await originRepository.ExistsAsync(
            response.Id, KnowledgeOriginType.Task, task.Id)).Should().BeTrue();
    }

    [Fact]
    public void Both_commands_require_Operate_roles()
    {
        new GenerateIncidentKnowledgeDraftCommand(Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new GenerateTaskKnowledgeDraftCommand(Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
    }
}
