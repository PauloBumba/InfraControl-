using InfraControl.Application.Network.Vlans;
using InfraControl.Application.Network.Vlans.Commands;
using InfraControl.Application.Network.Vlans.Queries;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Network;

/// <summary>
/// F3-T003: handlers do slice Vlan em Sqlite (sem boilerplate de using —
/// Fase 1 global usings). Vlan é entidade avulsa (sem guard de pai).
/// Mapeamento no envelope: ArgumentException → 400, KeyNotFound → 404,
/// InvalidOperation → 409 (número ou nome duplicado).
/// </summary>
public sealed class VlanCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public VlanCommandTests()
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

    private static CreateVlanCommandHandler CreateHandler(AppDbContext context)
        => new(new VlanRepository(context), NullLogger<CreateVlanCommandHandler>.Instance);

    private static UpdateVlanCommandHandler UpdateHandler(AppDbContext context)
        => new(new VlanRepository(context), NullLogger<UpdateVlanCommandHandler>.Instance);

    private static DeleteVlanCommandHandler DeleteHandler(AppDbContext context)
        => new(
            new VlanRepository(context),
            new SwitchPortVlanRepository(context),
            NullLogger<DeleteVlanCommandHandler>.Instance);

    private static SearchVlansQueryHandler SearchHandler(AppDbContext context)
        => new(new VlanRepository(context));

    private static GetVlanQueryHandler GetHandler(AppDbContext context)
        => new(new VlanRepository(context));

    // ── Create ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_Valid_ShouldReturnDto()
    {
        await using var context = await CreateContextAsync();

        var result = await CreateHandler(context).Handle(
            new CreateVlanCommand("Radiologia", 10, "Setor de imagem"), CancellationToken.None);

        result.Id.Should().NotBe(Guid.Empty);
        result.Name.Should().Be("Radiologia");
        result.VlanNumber.Should().Be(10);
        result.Description.Should().Be("Setor de imagem");
    }

    [Fact]
    public async Task Create_DuplicateNumber_ShouldThrowConflictMessage()
    {
        await using var context = await CreateContextAsync();
        await CreateHandler(context).Handle(
            new CreateVlanCommand("Radiologia", 10), CancellationToken.None);

        var act = () => CreateHandler(context).Handle(
            new CreateVlanCommand("Imagem", 10), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Já existe uma VLAN com o número '10'.");
    }

    [Fact]
    public async Task Create_DuplicateNameDifferentCase_ShouldThrowConflictMessage()
    {
        await using var context = await CreateContextAsync();
        await CreateHandler(context).Handle(
            new CreateVlanCommand("radiologia", 10), CancellationToken.None);

        var act = () => CreateHandler(context).Handle(
            new CreateVlanCommand("RADIOLOGIA", 20), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Já existe uma VLAN com o nome 'RADIOLOGIA'.");
    }

    [Fact]
    public async Task Create_EmptyName_ShouldThrowDomainMessage()
    {
        await using var context = await CreateContextAsync();

        var act = () => CreateHandler(context).Handle(
            new CreateVlanCommand("  ", 10), CancellationToken.None);

        (await act.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O nome da VLAN é obrigatório.*");
    }

    [Fact]
    public async Task Create_NumberOutOfRange_ShouldThrowDomainMessage()
    {
        await using var context = await CreateContextAsync();

        var act = () => CreateHandler(context).Handle(
            new CreateVlanCommand("Radiologia", 4095), CancellationToken.None);

        (await act.Should().ThrowAsync<ArgumentException>())
            .WithMessage("O número da VLAN deve estar entre 1 e 4094.*");
    }

    // ── Search ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_NoTerm_ShouldReturnAllOrderedByNumber()
    {
        await using var context = await CreateContextAsync();
        foreach (var (name, number) in new[] { ("Cameras", 20), ("Default", 1), ("Unifi", 100) })
            await CreateHandler(context).Handle(
                new CreateVlanCommand(name, number), CancellationToken.None);

        var result = await SearchHandler(context).Handle(
            new SearchVlansQuery(), CancellationToken.None);

        result.TotalCount.Should().Be(3);
        result.Items.Select(vlan => vlan.VlanNumber).Should().Equal(
            new[] { 1, 20, 100 },
            "ordenação por número da VLAN (ordem operacional 802.1Q)");
    }

    [Fact]
    public async Task Search_Term_ShouldFilterByNameOrDescription()
    {
        await using var context = await CreateContextAsync();
        await CreateHandler(context).Handle(
            new CreateVlanCommand("Radiologia", 10, "Setor de imagem"), CancellationToken.None);
        await CreateHandler(context).Handle(
            new CreateVlanCommand("Telefonia", 20, "Ramos PBX"), CancellationToken.None);

        var byName = await SearchHandler(context).Handle(
            new SearchVlansQuery("radio"), CancellationToken.None);
        var byDescription = await SearchHandler(context).Handle(
            new SearchVlansQuery("PBX"), CancellationToken.None);

        byName.TotalCount.Should().Be(1);
        byName.Items.Single().Name.Should().Be("Radiologia");
        byDescription.TotalCount.Should().Be(1);
        byDescription.Items.Single().Name.Should().Be("Telefonia");
    }

    [Fact]
    public async Task Search_PageAndSize_ShouldBeClamped()
    {
        await using var context = await CreateContextAsync();
        foreach (var (name, number) in new[] { ("Default", 1), ("Radiologia", 10), ("Telefonia", 20) })
            await CreateHandler(context).Handle(
                new CreateVlanCommand(name, number), CancellationToken.None);

        var result = await SearchHandler(context).Handle(
            new SearchVlansQuery(Page: 0, PageSize: 0), CancellationToken.None);

        result.Page.Should().Be(1);
        result.PageSize.Should().Be(1);
        result.TotalCount.Should().Be(3);
        result.Items.Should().HaveCount(1);
    }

    // ── GetById ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_Existing_ShouldReturn()
    {
        await using var context = await CreateContextAsync();
        var created = await CreateHandler(context).Handle(
            new CreateVlanCommand("Radiologia", 10), CancellationToken.None);

        var result = await GetHandler(context).Handle(
            new GetVlanQuery(created.Id), CancellationToken.None);

        result.Should().Be(created);
    }

    [Fact]
    public async Task GetById_Unknown_ShouldThrowNotFoundMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var act = () => GetHandler(context).Handle(
            new GetVlanQuery(unknownId), CancellationToken.None);

        (await act.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"VLAN '{unknownId}' não encontrada.");
    }

    // ── Update ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_Valid_ShouldApply()
    {
        await using var context = await CreateContextAsync();
        var created = await CreateHandler(context).Handle(
            new CreateVlanCommand("Radiologia", 10), CancellationToken.None);

        await UpdateHandler(context).Handle(
            new UpdateVlanCommand(created.Id, "Radiologia Norte", 15, "Setor de imagem"),
            CancellationToken.None);

        var updated = await GetHandler(context).Handle(
            new GetVlanQuery(created.Id), CancellationToken.None);
        updated.Name.Should().Be("Radiologia Norte");
        updated.VlanNumber.Should().Be(15);
        updated.Description.Should().Be("Setor de imagem");
    }

    [Fact]
    public async Task Update_Unknown_ShouldThrowNotFoundMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var act = () => UpdateHandler(context).Handle(
            new UpdateVlanCommand(unknownId, "Radiologia", 10), CancellationToken.None);

        (await act.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"VLAN '{unknownId}' não encontrada.");
    }

    [Fact]
    public async Task Update_NumberToExisting_ShouldThrowConflictMessage()
    {
        await using var context = await CreateContextAsync();
        await CreateHandler(context).Handle(
            new CreateVlanCommand("Radiologia", 10), CancellationToken.None);
        var other = await CreateHandler(context).Handle(
            new CreateVlanCommand("Telefonia", 20), CancellationToken.None);

        var act = () => UpdateHandler(context).Handle(
            new UpdateVlanCommand(other.Id, "Telefonia", 10), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Já existe uma VLAN com o número '10'.");
    }

    [Fact]
    public async Task Update_NameToExisting_ShouldThrowConflictMessage()
    {
        await using var context = await CreateContextAsync();
        await CreateHandler(context).Handle(
            new CreateVlanCommand("radiologia", 10), CancellationToken.None);
        var other = await CreateHandler(context).Handle(
            new CreateVlanCommand("Telefonia", 20), CancellationToken.None);

        var act = () => UpdateHandler(context).Handle(
            new UpdateVlanCommand(other.Id, "RADIOLOGIA", 20), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Já existe uma VLAN com o nome 'RADIOLOGIA'.");
    }

    [Fact]
    public async Task Update_ToItsOwnValues_ShouldSucceed()
    {
        await using var context = await CreateContextAsync();
        var created = await CreateHandler(context).Handle(
            new CreateVlanCommand("Radiologia", 10), CancellationToken.None);

        var act = () => UpdateHandler(context).Handle(
            new UpdateVlanCommand(created.Id, "RADIOLOGIA", 10), CancellationToken.None);

        await act.Should().NotThrowAsync();
        var updated = await GetHandler(context).Handle(
            new GetVlanQuery(created.Id), CancellationToken.None);
        updated.Name.Should().Be("RADIOLOGIA");
    }

    // ── Delete ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_Existing_ShouldRemove()
    {
        await using var context = await CreateContextAsync();
        var created = await CreateHandler(context).Handle(
            new CreateVlanCommand("Radiologia", 10), CancellationToken.None);

        await DeleteHandler(context).Handle(
            new DeleteVlanCommand(created.Id), CancellationToken.None);

        var act = () => GetHandler(context).Handle(
            new GetVlanQuery(created.Id), CancellationToken.None);
        (await act.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"VLAN '{created.Id}' não encontrada.");
    }

    [Fact]
    public async Task Delete_Unknown_ShouldThrowNotFoundMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var act = () => DeleteHandler(context).Handle(
            new DeleteVlanCommand(unknownId), CancellationToken.None);

        (await act.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"VLAN '{unknownId}' não encontrada.");
    }
}
