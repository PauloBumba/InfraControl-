using InfraControl.Application.Checklists.Commands;
using InfraControl.Application.Checklists.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Checklists;

/// <summary>
/// F50-T007/ADR-067: CRUD do checklist genérico espelhando o
/// TaskCriterion (F23-T003) — 404/400 pt-BR, no-op sem persistência,
/// dono polimórfico validado por tipo no handler (precedente
/// KnowledgeArticleOrigin), sem timeline (não existe timeline
/// genérica).
/// </summary>
public sealed class ChecklistCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public ChecklistCommandTests()
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

    private static AddChecklistItemCommandHandler AddHandler(AppDbContext context)
        => new(
            new DeviceRepository(context),
            new LocationRepository(context),
            new IncidentRepository(context),
            new ProjectRepository(context),
            new ChecklistItemRepository(context),
            NullLogger<AddChecklistItemCommandHandler>.Instance);

    private static UpdateChecklistItemCommandHandler UpdateHandler(AppDbContext context)
        => new(
            new ChecklistItemRepository(context),
            NullLogger<UpdateChecklistItemCommandHandler>.Instance);

    private static DeleteChecklistItemCommandHandler DeleteHandler(AppDbContext context)
        => new(
            new ChecklistItemRepository(context),
            NullLogger<DeleteChecklistItemCommandHandler>.Instance);

    private static GetChecklistItemsQueryHandler QueryHandler(AppDbContext context)
        => new(
            new DeviceRepository(context),
            new LocationRepository(context),
            new IncidentRepository(context),
            new ProjectRepository(context),
            new ChecklistItemRepository(context));

    private static async Task<Guid> SeedLocationAsync(AppDbContext context)
    {
        var location = Location.Create("Sala Checklist");
        context.Locations.Add(location);
        await context.SaveChangesAsync();
        return location.Id;
    }

    private static async Task<Guid> SeedDeviceAsync(AppDbContext context)
    {
        var locationId = await SeedLocationAsync(context);
        var device = Device.Create(
            locationId, "srv-checklist", DeviceType.Server, ip: "192.0.2.77");
        context.Devices.Add(device);
        await context.SaveChangesAsync();
        return device.Id;
    }

    private static async Task<Guid> SeedIncidentAsync(AppDbContext context)
    {
        var incident = Incident.Create("Chamado checklist", IncidentSeverity.Low);
        context.Incidents.Add(incident);
        await context.SaveChangesAsync();
        return incident.Id;
    }

    private static async Task<Guid> SeedProjectAsync(AppDbContext context)
    {
        var project = Project.Create(
            "PRJ-CL",
            "Projeto checklist",
            "Projeto para testar o checklist genérico.",
            Guid.NewGuid(),
            ProjectPriority.Medium);
        context.Projects.Add(project);
        await context.SaveChangesAsync();
        return project.Id;
    }

    // ── Autorização ───────────────────────────────────────────────────

    [Fact]
    public void Commands_ShouldRequireOperate()
    {
        new AddChecklistItemCommand("device", Guid.NewGuid(), "ok").RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new UpdateChecklistItemCommand(Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new DeleteChecklistItemCommand(Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new GetChecklistItemsQuery("device", Guid.NewGuid()).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Read);
    }

    // ── Criação ───────────────────────────────────────────────────────

    [Fact]
    public async Task Add_UnknownOwnerType_ShouldThrow400PtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => AddHandler(context).Handle(
            new AddChecklistItemCommand("banana", Guid.NewGuid(), "ok"),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage(
                "Tipo inválido: 'banana'. Use 'device', 'location', 'incident' ou 'project'.*");
    }

    [Theory]
    [InlineData("device", "Device")]
    [InlineData("location", "Local")]
    [InlineData("incident", "Incidente")]
    [InlineData("project", "Projeto")]
    public async Task Add_NonexistentOwner_ShouldThrow404PtBr(
        string ownerType, string noun)
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => AddHandler(context).Handle(
            new AddChecklistItemCommand(ownerType, unknown, "ok"),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"{noun} '{unknown}' não encontrado.");
    }

    [Fact]
    public async Task Add_ShouldPersistItemForDeviceOwner()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context);

        var dto = await AddHandler(context).Handle(
            new AddChecklistItemCommand(
                "device", deviceId, "  checklist de onboarding  "),
            CancellationToken.None);

        dto.OwnerType.Should().Be("Device");
        dto.OwnerId.Should().Be(deviceId);
        dto.Description.Should().Be("checklist de onboarding");
        dto.IsDone.Should().BeFalse();
        dto.DoneAt.Should().BeNull();

        (await context.ChecklistItems.AsNoTracking().ToListAsync())
            .Should().ContainSingle(item => item.Id == dto.Id);
    }

    [Fact]
    public async Task Add_BlankDescription_ShouldThrow400PtBr()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);

        var action = () => AddHandler(context).Handle(
            new AddChecklistItemCommand("location", locationId, "   "),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("A descrição do item é obrigatória.*");
    }

    // ── Atualização ───────────────────────────────────────────────────

    [Fact]
    public async Task Update_UnknownItem_ShouldThrow404PtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => UpdateHandler(context).Handle(
            new UpdateChecklistItemCommand(unknown, Done: true),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Item '{unknown}' não encontrado.");
    }

    [Fact]
    public async Task Update_ShouldEditDescriptionAndStampDoneAt()
    {
        await using var context = await CreateContextAsync();
        var incidentId = await SeedIncidentAsync(context);
        var added = await AddHandler(context).Handle(
            new AddChecklistItemCommand("incident", incidentId, "versão antiga"),
            CancellationToken.None);

        var updated = await UpdateHandler(context).Handle(
            new UpdateChecklistItemCommand(
                added.Id, Description: "versão nova", Done: true),
            CancellationToken.None);

        updated.Description.Should().Be("versão nova");
        updated.IsDone.Should().BeTrue();
        updated.DoneAt.Should().NotBeNull();

        var undone = await UpdateHandler(context).Handle(
            new UpdateChecklistItemCommand(added.Id, Done: false),
            CancellationToken.None);

        undone.Description.Should().Be("versão nova");
        undone.IsDone.Should().BeFalse();
        undone.DoneAt.Should().BeNull();
    }

    [Fact]
    public async Task Update_NullsShouldKeepCurrentValues()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context);
        var added = await AddHandler(context).Handle(
            new AddChecklistItemCommand("device", deviceId, "mantém este"),
            CancellationToken.None);
        await UpdateHandler(context).Handle(
            new UpdateChecklistItemCommand(added.Id, Done: true),
            CancellationToken.None);

        var dto = await UpdateHandler(context).Handle(
            new UpdateChecklistItemCommand(added.Id),
            CancellationToken.None);

        dto.Description.Should().Be("mantém este");
        dto.IsDone.Should().BeTrue();
        dto.DoneAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Update_NoChanges_ShouldNotPersist()
    {
        await using var context = await CreateContextAsync();
        var projectId = await SeedProjectAsync(context);
        var added = await AddHandler(context).Handle(
            new AddChecklistItemCommand("project", projectId, "sem mudança"),
            CancellationToken.None);
        var before = (await context.ChecklistItems.AsNoTracking()
            .SingleAsync(item => item.Id == added.Id)).UpdateDateTime;

        var dto = await UpdateHandler(context).Handle(
            new UpdateChecklistItemCommand(added.Id),
            CancellationToken.None);

        dto.Description.Should().Be("sem mudança");
        var after = (await context.ChecklistItems.AsNoTracking()
            .SingleAsync(item => item.Id == added.Id)).UpdateDateTime;
        after.Should().Be(before);
    }

    [Fact]
    public async Task Update_OverlongDescription_ShouldThrow400PtBr()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context);
        var added = await AddHandler(context).Handle(
            new AddChecklistItemCommand("device", deviceId, "curto"),
            CancellationToken.None);

        var action = () => UpdateHandler(context).Handle(
            new UpdateChecklistItemCommand(added.Id, new string('c', 301)),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("*máximo 300 caracteres*");
    }

    // ── Exclusão ──────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_ShouldRemoveItem()
    {
        await using var context = await CreateContextAsync();
        var deviceId = await SeedDeviceAsync(context);
        var added = await AddHandler(context).Handle(
            new AddChecklistItemCommand("device", deviceId, "descartado"),
            CancellationToken.None);

        await DeleteHandler(context).Handle(
            new DeleteChecklistItemCommand(added.Id),
            CancellationToken.None);

        (await context.ChecklistItems.AsNoTracking().ToListAsync())
            .Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_UnknownItem_ShouldThrow404PtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => DeleteHandler(context).Handle(
            new DeleteChecklistItemCommand(unknown),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Item '{unknown}' não encontrado.");
    }

    // ── Consulta ──────────────────────────────────────────────────────

    [Fact]
    public async Task Get_UnknownOwner_ShouldThrow404PtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => QueryHandler(context).Handle(
            new GetChecklistItemsQuery("device", unknown),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Device '{unknown}' não encontrado.");
    }

    [Fact]
    public async Task Get_ShouldReturnChronologicalByOwner()
    {
        await using var context = await CreateContextAsync();
        var locationId = await SeedLocationAsync(context);
        var otherLocationId = await SeedLocationAsync(context);
        await AddHandler(context).Handle(
            new AddChecklistItemCommand("location", locationId, "primeiro"),
            CancellationToken.None);
        await AddHandler(context).Handle(
            new AddChecklistItemCommand("location", locationId, "segundo"),
            CancellationToken.None);
        await AddHandler(context).Handle(
            new AddChecklistItemCommand("location", otherLocationId, "de outro dono"),
            CancellationToken.None);

        var rows = await QueryHandler(context).Handle(
            new GetChecklistItemsQuery("location", locationId),
            CancellationToken.None);

        rows.Select(row => row.Description).Should().Equal("primeiro", "segundo");
        rows.Should().OnlyContain(row => row.OwnerId == locationId);
    }
}
