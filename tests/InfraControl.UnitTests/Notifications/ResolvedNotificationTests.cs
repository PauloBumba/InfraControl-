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
/// F19-T007: IncidentResolvedDomainEvent e AlertResolvedDomainEvent viram
/// notificação com destinatários por ROLE — Operator e Admin (regra 7 da
/// revisão F19/F20/F21: só o InfraControl decide a resolução, o n8n não) —
/// deliveries resolvidas para os usuários dessas roles e idempotência via
/// inbox, no mesmo formato do IncidentCreatedDomainEvent.
/// </summary>
public sealed class ResolvedNotificationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public ResolvedNotificationTests()
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

    private static IncidentResolvedDomainEvent IncidentResolvedEvent(
        string title = "Carga de CPU alta")
        => new(Guid.NewGuid(), title, IncidentSeverity.Critical, Guid.NewGuid());

    private static AlertResolvedDomainEvent AlertResolvedEvent(
        string message = "Device 'sw-01' está fora do ar (ping falhou).")
        => new(Guid.NewGuid(), message, Guid.NewGuid());

    private static Mock<ConsumeContext<IncidentResolvedDomainEvent>> ContextFor(
        IncidentResolvedDomainEvent domainEvent)
    {
        var context = new Mock<ConsumeContext<IncidentResolvedDomainEvent>>();
        context.SetupGet(c => c.Message).Returns(domainEvent);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return context;
    }

    private static Mock<ConsumeContext<AlertResolvedDomainEvent>> ContextFor(
        AlertResolvedDomainEvent domainEvent)
    {
        var context = new Mock<ConsumeContext<AlertResolvedDomainEvent>>();
        context.SetupGet(c => c.Message).Returns(domainEvent);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return context;
    }

    [Fact]
    public async Task Consume_IncidentResolved_ShouldNotifyOperatorAndAdminRoles()
    {
        var (context, consumer) = await CreateAsync();

        await consumer.Consume(ContextFor(IncidentResolvedEvent()).Object);

        var notification = await context.Notifications.AsNoTracking().SingleAsync();
        notification.EventType.Should().Be(nameof(IncidentResolvedDomainEvent));
        notification.Title.Should().Be("Incidente resolvido");
        notification.Message.Should().Contain("Carga de CPU alta")
            .And.Contain(nameof(IncidentSeverity.Critical));

        var recipients = await context.NotificationRecipients.AsNoTracking().ToListAsync();
        recipients.Select(r => r.Role)
            .Should().BeEquivalentTo(new[] { "Operator", "Admin" });
        recipients.Should().OnlyContain(r => r.UserId == null);
    }

    [Fact]
    public async Task Consume_AlertResolved_ShouldNotifyOperatorAndAdminRoles()
    {
        var (context, consumer) = await CreateAsync();

        await consumer.Consume(ContextFor(AlertResolvedEvent()).Object);

        var notification = await context.Notifications.AsNoTracking().SingleAsync();
        notification.EventType.Should().Be(nameof(AlertResolvedDomainEvent));
        notification.Title.Should().Be("Alerta resolvido");
        notification.Message.Should().Contain("sw-01");

        var recipients = await context.NotificationRecipients.AsNoTracking().ToListAsync();
        recipients.Select(r => r.Role)
            .Should().BeEquivalentTo(new[] { "Operator", "Admin" });
        recipients.Should().OnlyContain(r => r.UserId == null);
    }

    [Fact]
    public async Task Consume_IncidentResolved_SameEventTwice_ShouldNotDuplicate()
    {
        var (context, consumer) = await CreateAsync();
        var consume = ContextFor(IncidentResolvedEvent());

        await consumer.Consume(consume.Object);
        await consumer.Consume(consume.Object);

        (await context.Notifications.AsNoTracking().CountAsync()).Should().Be(1);
        (await context.NotificationRecipients.AsNoTracking().CountAsync()).Should().Be(2);
        (await context.ProcessedMessages.AsNoTracking().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Consume_AlertResolved_SameEventTwice_ShouldNotDuplicate()
    {
        var (context, consumer) = await CreateAsync();
        var consume = ContextFor(AlertResolvedEvent());

        await consumer.Consume(consume.Object);
        await consumer.Consume(consume.Object);

        (await context.Notifications.AsNoTracking().CountAsync()).Should().Be(1);
        (await context.NotificationRecipients.AsNoTracking().CountAsync()).Should().Be(2);
        (await context.ProcessedMessages.AsNoTracking().CountAsync()).Should().Be(1);
    }
}
