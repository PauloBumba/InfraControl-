using FluentAssertions;
using InfraControl.Application.Incidents.Commands;
using InfraControl.Application.Incidents.Queries;
using InfraControl.Domain.Authorization;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Incidents;

/// <summary>
/// F6-T003: CRUD/estados do Incident — guards pt-BR (400/404) e a query
/// de listagem; permissões IncidentsRead/Manage.
/// </summary>
public sealed class IncidentCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public IncidentCommandTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    private async Task<AppDbContext> CreateContextAsync()
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static IncidentRepository Repo(AppDbContext context) => new(context);

    private static CreateIncidentCommandHandler CreateHandler(AppDbContext context)
        => new(Repo(context), NullLogger<CreateIncidentCommandHandler>.Instance);

    // ── Create ───────────────────────────────────────────────────────────

    [Fact]
    public void Create_ShouldRequireIncidentsManage()
        => new CreateIncidentCommand("t", IncidentSeverity.High).RequiredPermissions
            .Should().Equal(Permission.IncidentsManage.ToString());

    [Fact]
    public async Task Create_ShouldOpenIncidentAndReturnDto()
    {
        await using var context = await CreateContextAsync();

        var dto = await CreateHandler(context).Handle(
            new CreateIncidentCommand(
                "Switch principal fora do ar",
                IncidentSeverity.Critical,
                Description: "ping com streak"),
            CancellationToken.None);

        dto.Title.Should().Be("Switch principal fora do ar");
        dto.Status.Should().Be(nameof(IncidentStatus.Open));
        dto.Severity.Should().Be(nameof(IncidentSeverity.Critical));
        dto.AlertCount.Should().Be(0);

        var saved = await context.Incidents.AsNoTracking().SingleAsync();
        saved.Description.Should().Be("ping com streak");
    }

    [Fact]
    public async Task Create_WithoutTitle_ShouldThrowArgumentPtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => CreateHandler(context).Handle(
            new CreateIncidentCommand("  ", IncidentSeverity.Low),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O título do incidente é obrigatório.*");
    }

    // ── Acknowledge (docs/15) ────────────────────────────────────────────

    [Fact]
    public async Task Acknowledge_ShouldMoveToInvestigating()
    {
        await using var context = await CreateContextAsync();
        var created = await CreateHandler(context).Handle(
            new CreateIncidentCommand("inc", IncidentSeverity.High),
            CancellationToken.None);

        var dto = await new AcknowledgeIncidentCommandHandler(Repo(context)).Handle(
            new AcknowledgeIncidentCommand(created.Id), CancellationToken.None);

        dto.Status.Should().Be(nameof(IncidentStatus.Investigating));
    }

    [Fact]
    public async Task Acknowledge_UnknownIncident_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => new AcknowledgeIncidentCommandHandler(Repo(context)).Handle(
            new AcknowledgeIncidentCommand(unknown), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Incidente '{unknown}' não encontrado.");
    }

    // ── UpdateStatus ─────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateStatus_ShouldMoveAndStampResolvedAt()
    {
        await using var context = await CreateContextAsync();
        var created = await CreateHandler(context).Handle(
            new CreateIncidentCommand("inc", IncidentSeverity.Critical),
            CancellationToken.None);

        var handler = new UpdateIncidentStatusCommandHandler(Repo(context));

        var resolved = await handler.Handle(
            new UpdateIncidentStatusCommand(created.Id, "resolved"),
            CancellationToken.None);
        resolved.Status.Should().Be(nameof(IncidentStatus.Resolved));
        resolved.ResolvedAt.Should().NotBeNull();

        var reopened = await handler.Handle(
            new UpdateIncidentStatusCommand(created.Id, "monitoring"),
            CancellationToken.None);
        reopened.Status.Should().Be(nameof(IncidentStatus.Monitoring));
        reopened.ResolvedAt.Should().BeNull();
    }

    [Fact]
    public async Task UpdateStatus_InvalidValue_ShouldThrowArgumentPtBr()
    {
        await using var context = await CreateContextAsync();
        var created = await CreateHandler(context).Handle(
            new CreateIncidentCommand("inc", IncidentSeverity.Low),
            CancellationToken.None);

        var action = () => new UpdateIncidentStatusCommandHandler(Repo(context)).Handle(
            new UpdateIncidentStatusCommand(created.Id, "banana"),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*banana*");
    }

    [Fact]
    public async Task UpdateStatus_SameStatus_ShouldThrowArgumentPtBr()
    {
        await using var context = await CreateContextAsync();
        var created = await CreateHandler(context).Handle(
            new CreateIncidentCommand("inc", IncidentSeverity.Low),
            CancellationToken.None);

        var action = () => new UpdateIncidentStatusCommandHandler(Repo(context)).Handle(
            new UpdateIncidentStatusCommand(created.Id, "open"),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*já está no status*");
    }

    // ── Queries ──────────────────────────────────────────────────────────

    [Fact]
    public void Queries_ShouldRequireIncidentsRead()
    {
        new SearchIncidentsQuery().RequiredPermissions.Should()
            .Equal(Permission.IncidentsRead.ToString());
        new GetIncidentQuery(Guid.NewGuid()).RequiredPermissions.Should()
            .Equal(Permission.IncidentsRead.ToString());
    }

    [Fact]
    public async Task Search_ShouldFilterByStatusAndDeviceAndPaginate()
    {
        await using var context = await CreateContextAsync();
        var create = CreateHandler(context);
        var first = await create.Handle(
            new CreateIncidentCommand("a", IncidentSeverity.Critical),
            CancellationToken.None);
        await create.Handle(
            new CreateIncidentCommand("b", IncidentSeverity.Low),
            CancellationToken.None);

        await new UpdateIncidentStatusCommandHandler(Repo(context)).Handle(
            new UpdateIncidentStatusCommand(first.Id, "resolved"),
            CancellationToken.None);

        var open = await new SearchIncidentsQueryHandler(Repo(context)).Handle(
            new SearchIncidentsQuery(Status: "open"), CancellationToken.None);
        open.Items.Should().ContainSingle(i => i.Title == "b");

        var resolved = await new SearchIncidentsQueryHandler(Repo(context)).Handle(
            new SearchIncidentsQuery(Status: "resolved"), CancellationToken.None);
        resolved.Items.Should().ContainSingle(i => i.Title == "a");

        var filtered = await new SearchIncidentsQueryHandler(Repo(context)).Handle(
            new SearchIncidentsQuery(DeviceId: Guid.NewGuid()),
            CancellationToken.None);
        filtered.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_InvalidStatus_ShouldThrowArgumentPtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => new SearchIncidentsQueryHandler(Repo(context)).Handle(
            new SearchIncidentsQuery(Status: "banana"), CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*banana*");
    }

    [Fact]
    public async Task Get_UnknownIncident_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => new GetIncidentQueryHandler(Repo(context)).Handle(
            new GetIncidentQuery(unknown), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Incidente '{unknown}' não encontrado.");
    }
}
