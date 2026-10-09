using FluentAssertions;
using InfraControl.Application.Inventory.Devices.Commands;
using InfraControl.Application.Knowledge.Articles.Commands;
using InfraControl.Application.Knowledge.Articles.Queries;
using InfraControl.Application.Sla.Services.Commands;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Knowledge;

/// <summary>
/// F10-T004: vínculo Artigo ↔ Device/Incident/Service — 404 pt-BR
/// (artigo/alvo/vínculo), 409 em duplicidade, ids expostos no dto do
/// GET e CASCADE decidido pelo usuário: apagar device/service remove
/// só o vínculo, o artigo permanece.
/// </summary>
public sealed class KnowledgeArticleLinkCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public KnowledgeArticleLinkCommandTests()
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

    private static KnowledgeArticleRepository ArticleRepo(AppDbContext c) => new(c);
    private static DeviceRepository DeviceRepo(AppDbContext c) => new(c);
    private static IncidentRepository IncidentRepo(AppDbContext c) => new(c);
    private static ServiceRepository ServiceRepo(AppDbContext c) => new(c);
    private static KnowledgeArticleDeviceRepository DeviceLinkRepo(AppDbContext c) => new(c);
    private static KnowledgeArticleIncidentRepository IncidentLinkRepo(AppDbContext c) => new(c);
    private static KnowledgeArticleServiceRepository ServiceLinkRepo(AppDbContext c) => new(c);

    private static GetKnowledgeArticleQueryHandler GetHandler(AppDbContext context)
        => new(
            ArticleRepo(context),
            DeviceLinkRepo(context),
            IncidentLinkRepo(context),
            ServiceLinkRepo(context),
            new KnowledgeArticleOriginRepository(context),
            new FakeCurrentUser());

    private sealed class FakeCurrentUser : ICurrentUserContext
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public string? UserName => "operador@exemplo.test";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles { get; } = ["Operator"];
    }

    private static LinkKnowledgeArticleDeviceCommandHandler LinkDeviceHandler(AppDbContext c)
        => new(
            ArticleRepo(c), DeviceRepo(c), DeviceLinkRepo(c),
            NullLogger<LinkKnowledgeArticleDeviceCommandHandler>.Instance);

    private static UnlinkKnowledgeArticleDeviceCommandHandler UnlinkDeviceHandler(AppDbContext c)
        => new(
            ArticleRepo(c), DeviceLinkRepo(c),
            NullLogger<UnlinkKnowledgeArticleDeviceCommandHandler>.Instance);

    private static LinkKnowledgeArticleIncidentCommandHandler LinkIncidentHandler(AppDbContext c)
        => new(
            ArticleRepo(c), IncidentRepo(c), IncidentLinkRepo(c),
            NullLogger<LinkKnowledgeArticleIncidentCommandHandler>.Instance);

    private static UnlinkKnowledgeArticleIncidentCommandHandler UnlinkIncidentHandler(AppDbContext c)
        => new(
            ArticleRepo(c), IncidentLinkRepo(c),
            NullLogger<UnlinkKnowledgeArticleIncidentCommandHandler>.Instance);

    private static LinkKnowledgeArticleServiceCommandHandler LinkServiceHandler(AppDbContext c)
        => new(
            ArticleRepo(c), ServiceRepo(c), ServiceLinkRepo(c),
            NullLogger<LinkKnowledgeArticleServiceCommandHandler>.Instance);

    private static UnlinkKnowledgeArticleServiceCommandHandler UnlinkServiceHandler(AppDbContext c)
        => new(
            ArticleRepo(c), ServiceLinkRepo(c),
            NullLogger<UnlinkKnowledgeArticleServiceCommandHandler>.Instance);

    private static async Task<(Guid ArticleId, Guid DeviceId, Guid IncidentId, Guid ServiceId)>
        SeedAllAsync(AppDbContext context, string ip = "192.0.2.74")
    {
        var article = KnowledgeArticle.Create("Artigo de vinculo", "Conteudo.", null, "Guia");
        context.KnowledgeArticles.Add(article);

        var location = Location.Create("Sala F10T004");
        context.Locations.Add(location);
        await context.SaveChangesAsync();

        var device = Device.Create(location.Id, "sw-f10t004", DeviceType.Switch, ip: ip);
        context.Devices.Add(device);

        var incident = Incident.Create("Incidente de vinculo", IncidentSeverity.Low);
        context.Incidents.Add(incident);

        var service = Service.Create("servico de vinculo");
        context.Services.Add(service);

        await context.SaveChangesAsync();
        return (article.Id, device.Id, incident.Id, service.Id);
    }

    // ── Permissões ────────────────────────────────────────────────────────

    [Fact]
    public void Commands_ShouldRequireOperate()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        new LinkKnowledgeArticleDeviceCommand(a, b).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new UnlinkKnowledgeArticleDeviceCommand(a, b).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new LinkKnowledgeArticleIncidentCommand(a, b).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new UnlinkKnowledgeArticleIncidentCommand(a, b).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new LinkKnowledgeArticleServiceCommand(a, b).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
        new UnlinkKnowledgeArticleServiceCommand(a, b).RequiredRoles
            .Should().BeEquivalentTo(RoleAccess.Operate);
    }

    // ── Device ────────────────────────────────────────────────────────────

    [Fact]
    public async Task LinkDevice_ShouldExposeInArticleDto()
    {
        await using var context = await CreateContextAsync();
        var (articleId, deviceId, _, _) = await SeedAllAsync(context);

        await LinkDeviceHandler(context).Handle(
            new LinkKnowledgeArticleDeviceCommand(articleId, deviceId),
            CancellationToken.None);

        var dto = await GetHandler(context).Handle(
            new GetKnowledgeArticleQuery(articleId), CancellationToken.None);
        dto.DeviceIds.Should().Equal(deviceId);
    }

    [Fact]
    public async Task LinkDevice_UnknownArticle_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var (_, deviceId, _, _) = await SeedAllAsync(context);
        var unknown = Guid.NewGuid();

        var action = () => LinkDeviceHandler(context).Handle(
            new LinkKnowledgeArticleDeviceCommand(unknown, deviceId),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Artigo '{unknown}' não encontrado.");
    }

    [Fact]
    public async Task LinkDevice_UnknownDevice_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var (articleId, _, _, _) = await SeedAllAsync(context);
        var unknown = Guid.NewGuid();

        var action = () => LinkDeviceHandler(context).Handle(
            new LinkKnowledgeArticleDeviceCommand(articleId, unknown),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Device '{unknown}' não encontrado.");
    }

    [Fact]
    public async Task LinkDevice_Duplicate_ShouldThrowConflictPtBr()
    {
        await using var context = await CreateContextAsync();
        var (articleId, deviceId, _, _) = await SeedAllAsync(context);

        await LinkDeviceHandler(context).Handle(
            new LinkKnowledgeArticleDeviceCommand(articleId, deviceId),
            CancellationToken.None);

        var action = () => LinkDeviceHandler(context).Handle(
            new LinkKnowledgeArticleDeviceCommand(articleId, deviceId),
            CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("O device já está vinculado ao artigo.");
    }

    [Fact]
    public async Task UnlinkDevice_ShouldRemoveLinkAndClearDto()
    {
        await using var context = await CreateContextAsync();
        var (articleId, deviceId, _, _) = await SeedAllAsync(context);
        await LinkDeviceHandler(context).Handle(
            new LinkKnowledgeArticleDeviceCommand(articleId, deviceId),
            CancellationToken.None);

        await UnlinkDeviceHandler(context).Handle(
            new UnlinkKnowledgeArticleDeviceCommand(articleId, deviceId),
            CancellationToken.None);

        var dto = await GetHandler(context).Handle(
            new GetKnowledgeArticleQuery(articleId), CancellationToken.None);
        dto.DeviceIds.Should().BeEmpty();
    }

    [Fact]
    public async Task UnlinkDevice_UnknownLink_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var (articleId, _, _, _) = await SeedAllAsync(context);
        var stranger = Guid.NewGuid();

        var action = () => UnlinkDeviceHandler(context).Handle(
            new UnlinkKnowledgeArticleDeviceCommand(articleId, stranger),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Vínculo do artigo '{articleId}' com o device "
                + $"'{stranger}' não encontrado.");
    }

    // ── Incident ──────────────────────────────────────────────────────────

    [Fact]
    public async Task LinkIncident_ShouldExposeInArticleDto()
    {
        await using var context = await CreateContextAsync();
        var (articleId, _, incidentId, _) = await SeedAllAsync(context);

        await LinkIncidentHandler(context).Handle(
            new LinkKnowledgeArticleIncidentCommand(articleId, incidentId),
            CancellationToken.None);

        var dto = await GetHandler(context).Handle(
            new GetKnowledgeArticleQuery(articleId), CancellationToken.None);
        dto.IncidentIds.Should().Equal(incidentId);
    }

    [Fact]
    public async Task LinkIncident_UnknownIncident_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var (articleId, _, _, _) = await SeedAllAsync(context);
        var unknown = Guid.NewGuid();

        var action = () => LinkIncidentHandler(context).Handle(
            new LinkKnowledgeArticleIncidentCommand(articleId, unknown),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Incidente '{unknown}' não encontrado.");
    }

    [Fact]
    public async Task UnlinkIncident_UnknownLink_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var (articleId, _, _, _) = await SeedAllAsync(context);
        var stranger = Guid.NewGuid();

        var action = () => UnlinkIncidentHandler(context).Handle(
            new UnlinkKnowledgeArticleIncidentCommand(articleId, stranger),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Vínculo do artigo '{articleId}' com o incidente "
                + $"'{stranger}' não encontrado.");
    }

    // ── Service ───────────────────────────────────────────────────────────

    [Fact]
    public async Task LinkService_ShouldExposeInArticleDto()
    {
        await using var context = await CreateContextAsync();
        var (articleId, _, _, serviceId) = await SeedAllAsync(context);

        await LinkServiceHandler(context).Handle(
            new LinkKnowledgeArticleServiceCommand(articleId, serviceId),
            CancellationToken.None);

        var dto = await GetHandler(context).Handle(
            new GetKnowledgeArticleQuery(articleId), CancellationToken.None);
        dto.ServiceIds.Should().Equal(serviceId);
    }

    [Fact]
    public async Task LinkService_UnknownService_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var (articleId, _, _, _) = await SeedAllAsync(context);
        var unknown = Guid.NewGuid();

        var action = () => LinkServiceHandler(context).Handle(
            new LinkKnowledgeArticleServiceCommand(articleId, unknown),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Serviço '{unknown}' não encontrado.");
    }

    [Fact]
    public async Task UnlinkService_UnknownLink_ShouldThrowNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var (articleId, _, _, _) = await SeedAllAsync(context);
        var stranger = Guid.NewGuid();

        var action = () => UnlinkServiceHandler(context).Handle(
            new UnlinkKnowledgeArticleServiceCommand(articleId, stranger),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Vínculo do artigo '{articleId}' com o serviço "
                + $"'{stranger}' não encontrado.");
    }

    // ── Cascade decidido pelo usuário (30/09/2026) ────────────────────────

    /// <summary>Apagar device remove SÓ o vínculo — o artigo permanece.</summary>
    [Fact]
    public async Task DeleteDevice_WithLinkedArticle_ShouldCascadeLinkAndKeepArticle()
    {
        await using var context = await CreateContextAsync();
        var (articleId, deviceId, _, _) = await SeedAllAsync(context);
        await LinkDeviceHandler(context).Handle(
            new LinkKnowledgeArticleDeviceCommand(articleId, deviceId),
            CancellationToken.None);

        await new DeleteDeviceCommandHandler(
                DeviceRepo(context), NullLogger<DeleteDeviceCommandHandler>.Instance)
            .Handle(new DeleteDeviceCommand(deviceId), CancellationToken.None);

        var dto = await GetHandler(context).Handle(
            new GetKnowledgeArticleQuery(articleId), CancellationToken.None);
        dto.Id.Should().Be(articleId);
        dto.DeviceIds.Should().BeEmpty();
    }

    /// <summary>Mesmo cascade do lado de service.</summary>
    [Fact]
    public async Task DeleteService_WithLinkedArticle_ShouldCascadeLinkAndKeepArticle()
    {
        await using var context = await CreateContextAsync();
        var (articleId, _, _, serviceId) = await SeedAllAsync(context);
        await LinkServiceHandler(context).Handle(
            new LinkKnowledgeArticleServiceCommand(articleId, serviceId),
            CancellationToken.None);

        await new DeleteServiceCommandHandler(
                ServiceRepo(context),
                new SlaPolicyRepository(context),
                new SloRepository(context),
                NullLogger<DeleteServiceCommandHandler>.Instance)
            .Handle(new DeleteServiceCommand(serviceId), CancellationToken.None);

        var dto = await GetHandler(context).Handle(
            new GetKnowledgeArticleQuery(articleId), CancellationToken.None);
        dto.Id.Should().Be(articleId);
        dto.ServiceIds.Should().BeEmpty();
    }
}
