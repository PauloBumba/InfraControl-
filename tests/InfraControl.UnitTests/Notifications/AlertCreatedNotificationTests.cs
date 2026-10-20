using FluentAssertions;
using InfraControl.Application.Notifications.Channels;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Domain.Events;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using InfraControl.Workers.Consumers.Notifications;
using MassTransit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace InfraControl.UnitTests.Notifications;

/// <summary>
/// F33-T004/ADR-059 §decisões 2/3: AlertCreatedDomainEvent (só Critical)
/// vira notificação para as roles Operator e Admin — "Alerta criado"; a
/// 2ª mensagem "Incidente aberto" é do IncidentCreatedDomainEvent
/// separado (2 mensagens distintas para o mesmo problema) — idempotência
/// via inbox no mesmo formato do AlertResolved.
/// </summary>
public sealed class AlertCreatedNotificationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public AlertCreatedNotificationTests()
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
        var consumer = new NotificationEventConsumer(
            context,
            new InboxStore(context),
            Options.Create(new NotificationChannelsOptions()),
            NullLogger<NotificationEventConsumer>.Instance);
        return (context, consumer);
    }

    private static AlertCreatedDomainEvent AlertCreatedEvent(
        string message = "Device 'sw-01' está fora do ar (ping falhou).")
        => new(Guid.NewGuid(), AlertSeverity.Critical, Guid.NewGuid(), message);

    private static Mock<ConsumeContext<AlertCreatedDomainEvent>> ContextFor(
        AlertCreatedDomainEvent domainEvent)
    {
        var context = new Mock<ConsumeContext<AlertCreatedDomainEvent>>();
        context.SetupGet(c => c.Message).Returns(domainEvent);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return context;
    }

    [Fact]
    public async Task Consume_AlertCreated_ShouldNotifyOperatorAndAdminRoles()
    {
        var (context, consumer) = await CreateAsync();

        await consumer.Consume(ContextFor(AlertCreatedEvent()).Object);

        var notification = await context.Notifications.AsNoTracking().SingleAsync();
        notification.EventType.Should().Be(nameof(AlertCreatedDomainEvent));
        notification.Title.Should().Be("Alerta criado");
        notification.Message.Should().Contain("sw-01")
            .And.Contain(nameof(AlertSeverity.Critical));

        var recipients = await context.NotificationRecipients.AsNoTracking().ToListAsync();
        recipients.Select(r => r.Role)
            .Should().BeEquivalentTo(new[] { "Operator", "Admin" });
        recipients.Should().OnlyContain(r => r.UserId == null);
    }

    [Fact]
    public async Task Consume_AlertCreated_SameEventTwice_ShouldNotDuplicate()
    {
        var (context, consumer) = await CreateAsync();
        var consume = ContextFor(AlertCreatedEvent());

        await consumer.Consume(consume.Object);
        await consumer.Consume(consume.Object);

        (await context.Notifications.AsNoTracking().CountAsync()).Should().Be(1);
        (await context.NotificationRecipients.AsNoTracking().CountAsync()).Should().Be(2);
        (await context.ProcessedMessages.AsNoTracking().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Consume_AlertCreated_DifferentEvents_ShouldCreateBothNotifications()
    {
        var (context, consumer) = await CreateAsync();

        await consumer.Consume(ContextFor(AlertCreatedEvent("problema A")).Object);
        await consumer.Consume(ContextFor(AlertCreatedEvent("problema B")).Object);

        (await context.Notifications.AsNoTracking().CountAsync()).Should().Be(2);
        (await context.ProcessedMessages.AsNoTracking().CountAsync()).Should().Be(2);
    }
}
