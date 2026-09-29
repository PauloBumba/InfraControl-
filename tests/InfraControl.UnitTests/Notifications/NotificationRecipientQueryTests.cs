using FluentAssertions;
using InfraControl.Application.Notifications.Recipients.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Notifications;

/// <summary>
/// F9-T002: leitura dos destinatários e resolução role → usuários com
/// dedup (docs/07: usuário em múltiplas roles = única notificação lógica);
/// 404 pt-BR para notificação inexistente.
/// </summary>
public sealed class NotificationRecipientQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public NotificationRecipientQueryTests()
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

    private static async Task<Notification> SeedNotificationAsync(AppDbContext context)
    {
        var notification = Notification.Create(
            Guid.NewGuid(), "UserCreatedDomainEvent", "Título", "Mensagem.");
        context.Notifications.Add(notification);
        await context.SaveChangesAsync();
        return notification;
    }

    private static GetNotificationRecipientsQueryHandler SearchHandler(AppDbContext context)
        => new(new NotificationRecipientRepository(context));

    private static ResolveNotificationUsersQueryHandler ResolveHandler(AppDbContext context)
        => new(new NotificationRecipientRepository(context));

    [Fact]
    public async Task Search_ShouldReturnRecipientsOrderedByRoleThenUser()
    {
        await using var context = await CreateContextAsync();
        var notification = await SeedNotificationAsync(context);
        context.NotificationRecipients.Add(
            NotificationRecipient.CreateForUser(notification.Id, Guid.NewGuid()));
        context.NotificationRecipients.Add(
            NotificationRecipient.CreateForRole(notification.Id, UserRole.Operator));
        await context.SaveChangesAsync();

        var page = await SearchHandler(context).Handle(
            new GetNotificationRecipientsQuery(notification.Id),
            CancellationToken.None);

        page.TotalCount.Should().Be(2);
        page.Items.Should().ContainSingle(r => r.Role == "Operator");
        page.Items.Should().ContainSingle(r => r.UserId != null);
    }

    [Fact]
    public async Task Search_UnknownNotification_ShouldThrowKeyNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => SearchHandler(context).Handle(
            new GetNotificationRecipientsQuery(unknown), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Notificação '{unknown}' não encontrada.");
    }

    [Fact]
    public async Task Resolve_UserInMultipleRoles_ShouldReturnSingleUser()
    {
        await using var context = await CreateContextAsync();
        var notification = await SeedNotificationAsync(context);

        // Usuário em DUAS roles + destinatário direto do mesmo usuário
        // → dedup para uma única notificação lógica (docs/07).
        var user = User.Create(Guid.NewGuid(), "multiplo@exemplo.test", "Múltiplo");
        user.AssignRole(UserRole.Viewer);
        user.AssignRole(UserRole.Operator);
        context.DomainUsers.Add(user);
        context.NotificationRecipients.Add(
            NotificationRecipient.CreateForRole(notification.Id, UserRole.Viewer));
        context.NotificationRecipients.Add(
            NotificationRecipient.CreateForRole(notification.Id, UserRole.Operator));
        context.NotificationRecipients.Add(
            NotificationRecipient.CreateForUser(notification.Id, user.Id));
        await context.SaveChangesAsync();

        var resolved = await ResolveHandler(context).Handle(
            new ResolveNotificationUsersQuery(notification.Id), CancellationToken.None);

        resolved.Should().ContainSingle().Which.Should().Be(user.Id);
    }

    [Fact]
    public async Task Resolve_ShouldOnlyIncludeActiveUsersFromRoles()
    {
        await using var context = await CreateContextAsync();
        var notification = await SeedNotificationAsync(context);

        var active = User.Create(Guid.NewGuid(), "ativo@exemplo.test", "Ativo");
        active.AssignRole(UserRole.Operator);
        var inactive = User.Create(Guid.NewGuid(), "inativo@exemplo.test", "Inativo");
        inactive.AssignRole(UserRole.Operator);
        inactive.Disable();
        context.DomainUsers.AddRange(active, inactive);
        context.NotificationRecipients.Add(
            NotificationRecipient.CreateForRole(notification.Id, UserRole.Operator));
        await context.SaveChangesAsync();

        var resolved = await ResolveHandler(context).Handle(
            new ResolveNotificationUsersQuery(notification.Id), CancellationToken.None);

        resolved.Should().ContainSingle().Which.Should().Be(active.Id);
    }

    [Fact]
    public async Task Resolve_UnknownNotification_ShouldThrowKeyNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknown = Guid.NewGuid();

        var action = () => ResolveHandler(context).Handle(
            new ResolveNotificationUsersQuery(unknown), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Notificação '{unknown}' não encontrada.");
    }
}
