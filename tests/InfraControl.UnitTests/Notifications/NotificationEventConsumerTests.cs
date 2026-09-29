using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Events;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using InfraControl.Workers.Consumers.Notifications;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Notifications;

/// <summary>
/// ADR-012/docs/07: o Notification Consumer recebe o evento do RabbitMQ,
/// é idempotente via ProcessedMessage (EventId + Consumer, docs/06) e cria
/// a Notification no PostgreSQL.
/// </summary>
public sealed class NotificationEventConsumerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public NotificationEventConsumerTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    private async Task<(AppDbContext Context, NotificationEventConsumer Consumer)> CreateAsync()
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
        var consumer = new NotificationEventConsumer(context, new InboxStore(context), NullLogger<NotificationEventConsumer>.Instance);
        return (context, consumer);
    }

    [Fact]
    public async Task Consume_UserCreated_ShouldCreateNotificationWithUserRecipient()
    {
        var (context, consumer) = await CreateAsync();
        var domainEvent = new UserCreatedDomainEvent(Guid.NewGuid(), "user@example.com");

        await consumer.HandleAsync(
            domainEvent.EventId, domainEvent.EventType,
            "Usuário criado", $"O usuário {domainEvent.Email} foi criado.",
            recipientUserId: domainEvent.UserId, recipientRole: null,
            CancellationToken.None);

        var notifications = await context.Notifications.AsNoTracking().ToListAsync();
        notifications.Should().ContainSingle();
        notifications[0].EventId.Should().Be(domainEvent.EventId);
        notifications[0].EventType.Should().Be("UserCreatedDomainEvent");
        notifications[0].Title.Should().Be("Usuário criado");
        notifications[0].Message.Should().Contain("user@example.com");

        var recipient = await context.NotificationRecipients.AsNoTracking().SingleAsync();
        recipient.NotificationId.Should().Be(notifications[0].Id);
        recipient.UserId.Should().Be(domainEvent.UserId);
        recipient.Role.Should().BeNull();
    }

    [Fact]
    public async Task Consume_SameEventTwice_ShouldNotDuplicate()
    {
        var (context, consumer) = await CreateAsync();
        var domainEvent = new UserDisabledDomainEvent(Guid.NewGuid());

        var handle = () => consumer.HandleAsync(
            domainEvent.EventId, domainEvent.EventType,
            "Usuário desativado", $"O usuário {domainEvent.UserId} foi desativado.",
            recipientUserId: domainEvent.UserId, recipientRole: null,
            CancellationToken.None);

        await handle();
        await handle();

        (await context.Notifications.AsNoTracking().CountAsync()).Should().Be(1);
        (await context.NotificationRecipients.AsNoTracking().CountAsync()).Should().Be(1);
        (await context.ProcessedMessages.AsNoTracking().CountAsync())
            .Should().Be(1);
    }

    [Fact]
    public async Task Consume_RoleAssigned_ShouldCarryRoleInMessage()
    {
        var (context, consumer) = await CreateAsync();
        var userId = Guid.NewGuid();
        var domainEvent = new UserRoleAssignedDomainEvent(userId, UserRole.Viewer);

        await consumer.HandleAsync(
            domainEvent.EventId, domainEvent.EventType,
            "Perfil atribuído", $"O perfil {domainEvent.Role} foi atribuído ao usuário {domainEvent.UserId}.",
            recipientUserId: null, recipientRole: domainEvent.Role.ToString(),
            CancellationToken.None);

        var notification = await context.Notifications.AsNoTracking().SingleAsync();
        notification.Message.Should().Contain("Viewer");
        notification.Message.Should().Contain(userId.ToString());

        // F9-T002: evento de role → destinatário é a ROLE, não o usuário.
        var recipient = await context.NotificationRecipients.AsNoTracking().SingleAsync();
        recipient.Role.Should().Be("Viewer");
        recipient.UserId.Should().BeNull();
    }

    [Fact]
    public async Task Consume_InvitationAccepted_ShouldResolveUserByEmail()
    {
        var (context, consumer) = await CreateAsync();
        var user = User.Create(Guid.NewGuid(), "convite@exemplo.test", "Convite");
        context.DomainUsers.Add(user);
        await context.SaveChangesAsync();

        var domainEvent = new InvitationAcceptedDomainEvent(
            Guid.NewGuid(), "convite@exemplo.test");

        await consumer.HandleInvitationAcceptedAsync(domainEvent, CancellationToken.None);

        var notification = await context.Notifications.AsNoTracking().SingleAsync();
        notification.Message.Should().Contain("convite@exemplo.test");

        var recipient = await context.NotificationRecipients.AsNoTracking().SingleAsync();
        recipient.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task Consume_InvitationAccepted_UnknownEmail_ShouldCreateWithoutRecipient()
    {
        var (context, consumer) = await CreateAsync();
        var domainEvent = new InvitationAcceptedDomainEvent(
            Guid.NewGuid(), "desconhecido@exemplo.test");

        await consumer.HandleInvitationAcceptedAsync(domainEvent, CancellationToken.None);

        (await context.Notifications.AsNoTracking().CountAsync()).Should().Be(1);
        (await context.NotificationRecipients.AsNoTracking().CountAsync()).Should().Be(0);
    }
}
