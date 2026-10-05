using FluentAssertions;
using InfraControl.Application.Notifications.Feed.Queries;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Notifications;

/// <summary>
/// F18-T001: feed de notificações do usuário autenticado — destinatário
/// direto OU destinatário de role (dedup por notificação, docs/07), mais
/// recentes primeiro, clamp de paginação (page ≥ 1, pageSize 1..100) e
/// falha explícita quando o contexto não tem usuário. Sem estado de
/// leitura (decisão da task: entregar sem "não lidas").
/// </summary>
public sealed class NotificationFeedQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public NotificationFeedQueryTests()
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

    private static async Task<Notification> SeedNotificationAsync(
        AppDbContext context, string title, DateTime createdAt)
    {
        var notification = Notification.Create(
            Guid.NewGuid(), "IncidentCreatedDomainEvent", title, "Mensagem.");
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();

        // ApplyAuditFields do AppDbContext sobrescreve CreateDateTime de
        // toda entrada Added com AppTimeProvider.Current — a data
        // determinística do teste é gravada numa segunda passada (estado
        // Modified não toca CreateDateTime, só UpdateDateTime).
        notification.CreateDateTime = createdAt;
        await context.SaveChangesAsync();
        return notification;
    }

    private sealed class FakeCurrentUser : ICurrentUserContext
    {
        public FakeCurrentUser(Guid? userId, params string[] roles)
        {
            UserId = userId;
            Roles = roles;
        }

        public Guid? UserId { get; }
        public string? UserName => "feed-test";
        public bool IsAuthenticated => UserId is not null;
        public IReadOnlyCollection<string> Roles { get; }
    }

    private static GetNotificationFeedQueryHandler Handler(
        AppDbContext context, FakeCurrentUser user)
        => new(new NotificationFeedRepository(context), user);

    [Fact]
    public async Task Feed_ShouldReturnOnlyOwnNotificationsNewestFirst()
    {
        await using var context = await CreateContextAsync();
        var me = Guid.NewGuid();
        var stranger = Guid.NewGuid();

        var antiga = await SeedNotificationAsync(context, "Antiga",
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var recente = await SeedNotificationAsync(context, "Recente",
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        var deOutro = await SeedNotificationAsync(context, "De outro",
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

        context.NotificationRecipients.Add(
            NotificationRecipient.CreateForUser(antiga.Id, me));
        context.NotificationRecipients.Add(
            NotificationRecipient.CreateForUser(recente.Id, me));
        context.NotificationRecipients.Add(
            NotificationRecipient.CreateForUser(deOutro.Id, stranger));
        await context.SaveChangesAsync();

        var page = await Handler(context, new FakeCurrentUser(me)).Handle(
            new GetNotificationFeedQuery(), CancellationToken.None);

        page.TotalCount.Should().Be(2);
        page.Items.Select(n => n.Title).Should().Equal("Recente", "Antiga");
        page.Items.Should().NotContain(n => n.Title == "De outro");
    }

    [Fact]
    public async Task Feed_ShouldIncludeRoleRecipients_AndExcludeOtherRoles()
    {
        await using var context = await CreateContextAsync();
        var me = Guid.NewGuid();

        var porRole = await SeedNotificationAsync(context, "Por role",
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        var outraRole = await SeedNotificationAsync(context, "Outra role",
            new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc));

        context.NotificationRecipients.Add(
            NotificationRecipient.CreateForRole(porRole.Id, Domain.Enums.UserRole.Operator));
        context.NotificationRecipients.Add(
            NotificationRecipient.CreateForRole(outraRole.Id, Domain.Enums.UserRole.Admin));
        await context.SaveChangesAsync();

        var page = await Handler(context, new FakeCurrentUser(me, "Operator")).Handle(
            new GetNotificationFeedQuery(), CancellationToken.None);

        page.TotalCount.Should().Be(1);
        page.Items.Single().Title.Should().Be("Por role");
    }

    [Fact]
    public async Task Feed_DirectAndRoleMatch_ShouldDeduplicateToSingleItem()
    {
        await using var context = await CreateContextAsync();
        var me = Guid.NewGuid();

        // O mesmo usuário como destinatário direto E pela role do
        // alvo → uma única notificação lógica no feed (docs/07).
        var notification = await SeedNotificationAsync(context, "Dedup",
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        context.NotificationRecipients.Add(
            NotificationRecipient.CreateForUser(notification.Id, me));
        context.NotificationRecipients.Add(
            NotificationRecipient.CreateForRole(
                notification.Id, Domain.Enums.UserRole.Operator));
        await context.SaveChangesAsync();

        var page = await Handler(context, new FakeCurrentUser(me, "Operator")).Handle(
            new GetNotificationFeedQuery(), CancellationToken.None);

        page.TotalCount.Should().Be(1);
        page.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Feed_ShouldClampPageAndPageSize()
    {
        await using var context = await CreateContextAsync();
        var me = Guid.NewGuid();

        var notification = await SeedNotificationAsync(context, "Única",
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        context.NotificationRecipients.Add(
            NotificationRecipient.CreateForUser(notification.Id, me));
        await context.SaveChangesAsync();

        var page = await Handler(context, new FakeCurrentUser(me)).Handle(
            new GetNotificationFeedQuery(Page: 0, PageSize: 500),
            CancellationToken.None);

        page.Page.Should().Be(1, "page abaixo do mínimo sobe para 1");
        page.PageSize.Should().Be(100, "pageSize é limitado ao clamp do projeto (1..100)");
        page.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Feed_WithoutCurrentUser_ShouldThrowUnauthorizedPtBr()
    {
        await using var context = await CreateContextAsync();

        var action = () => Handler(context, new FakeCurrentUser(null)).Handle(
            new GetNotificationFeedQuery(), CancellationToken.None);

        (await action.Should().ThrowAsync<UnauthorizedAccessException>())
            .WithMessage("Usuário autenticado é obrigatório para o feed de notificações.");
    }

    // ADR-030 §2 — GET /notifications/{id}: mesma visibilidade do feed
    // (destinatário direto OU de role); fora da visibilidade = 404.

    [Fact]
    public async Task GetNotification_WhenDirectRecipient_ShouldReturnFeedItem()
    {
        await using var context = await CreateContextAsync();
        var me = Guid.NewGuid();
        var notification = await SeedNotificationAsync(context, "Ponto único", DateTime.UtcNow);
        context.NotificationRecipients.Add(
            Domain.Entities.NotificationRecipient.CreateForUser(notification.Id, me));
        await context.SaveChangesAsync();

        var handler = new GetNotificationQueryHandler(
            new NotificationFeedRepository(context), new FakeCurrentUser(me, "Operator"));

        var item = await handler.Handle(
            new GetNotificationQuery(notification.Id), CancellationToken.None);

        item.Id.Should().Be(notification.Id);
        item.Title.Should().Be("Ponto único");
        item.EventType.Should().Be("IncidentCreatedDomainEvent");
    }

    [Fact]
    public async Task GetNotification_WhenVisibleByRole_ShouldReturnFeedItem()
    {
        await using var context = await CreateContextAsync();
        var notification = await SeedNotificationAsync(context, "Por role", DateTime.UtcNow);
        context.NotificationRecipients.Add(
            Domain.Entities.NotificationRecipient.CreateForRole(notification.Id, "Operator"));
        await context.SaveChangesAsync();

        var handler = new GetNotificationQueryHandler(
            new NotificationFeedRepository(context), new FakeCurrentUser(Guid.NewGuid(), "Operator"));

        var item = await handler.Handle(
            new GetNotificationQuery(notification.Id), CancellationToken.None);

        item.Id.Should().Be(notification.Id);
    }

    [Fact]
    public async Task GetNotification_WhenNotVisible_ShouldThrowKeyNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var notification = await SeedNotificationAsync(context, "Alheia", DateTime.UtcNow);

        var handler = new GetNotificationQueryHandler(
            new NotificationFeedRepository(context), new FakeCurrentUser(Guid.NewGuid(), "Viewer"));

        var action = () => handler.Handle(
            new GetNotificationQuery(notification.Id), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Notificação '{notification.Id}' não encontrada.");
    }
}
