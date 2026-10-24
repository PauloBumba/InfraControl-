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
/// F44-T001 (prompt master §8): transição de status do incidente
/// (IncidentUpdatedDomainEvent) vira notificação SignalR para Operator +
/// Admin — a central de operações atualiza sem F5. Resolução é PULADA:
/// ChangeStatus(Resolved) levanta também o IncidentResolvedDomainEvent e
/// a mensagem duplicada seria ruído (dedup do §6.3).
/// </summary>
public sealed class IncidentUpdatedNotificationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public IncidentUpdatedNotificationTests()
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

    private static IncidentUpdatedDomainEvent UpdatedEvent(IncidentStatus status)
        => new(Guid.NewGuid(), status);

    /// <summary>ConsumeContext simulado — só Message e CancellationToken são usados.</summary>
    private static Mock<ConsumeContext<IncidentUpdatedDomainEvent>> ContextFor(
        IncidentUpdatedDomainEvent domainEvent)
    {
        var context = new Mock<ConsumeContext<IncidentUpdatedDomainEvent>>();
        context.SetupGet(c => c.Message).Returns(domainEvent);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return context;
    }

    [Fact]
    public async Task Consume_Investigating_ShouldNotifyOperatorAndAdminViaSignalR()
    {
        var (context, consumer) = await CreateAsync();
        var operatorUserId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        context.DomainUsers.Add(User.Create(
            operatorUserId, "operador@exemplo.test", "Operador", UserRole.Operator));
        context.DomainUsers.Add(User.Create(
            adminUserId, "admin@exemplo.test", "Administrador", UserRole.Admin));
        await context.SaveChangesAsync();

        await consumer.Consume(ContextFor(
            UpdatedEvent(IncidentStatus.Investigating)).Object);

        var notification = await context.Notifications.AsNoTracking().SingleAsync();
        notification.EventType.Should().Be(nameof(IncidentUpdatedDomainEvent));
        notification.Title.Should().Be("Incidente atualizado");
        notification.Message.Should().Contain("Em investigação");

        var recipients = await context.NotificationRecipients.AsNoTracking().ToListAsync();
        recipients.Select(r => r.Role)
            .Should().BeEquivalentTo(new[] { "Operator", "Admin" });

        // default do evento = SignalR-only (a F42 pode trocar por regra)
        var deliveries = await context.NotificationDeliveries.AsNoTracking().ToListAsync();
        deliveries.Should().HaveCount(2);
        deliveries.Should().OnlyContain(d => d.Channel == NotificationChannel.SignalR);
    }

    [Fact]
    public async Task Consume_Resolved_ShouldSkipToAvoidDuplicateOfIncidentResolved()
    {
        var (context, consumer) = await CreateAsync();

        await consumer.Consume(ContextFor(
            UpdatedEvent(IncidentStatus.Resolved)).Object);

        (await context.Notifications.AsNoTracking().CountAsync()).Should().Be(0);
        (await context.NotificationRecipients.AsNoTracking().CountAsync()).Should().Be(0);
        (await context.NotificationDeliveries.AsNoTracking().CountAsync()).Should().Be(0);
        (await context.ProcessedMessages.AsNoTracking().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Consume_SameEventTwice_ShouldNotDuplicate()
    {
        var (context, consumer) = await CreateAsync();
        var consume = ContextFor(UpdatedEvent(IncidentStatus.Identified));

        await consumer.Consume(consume.Object);
        await consumer.Consume(consume.Object);

        (await context.Notifications.AsNoTracking().CountAsync()).Should().Be(1);
        (await context.ProcessedMessages.AsNoTracking().CountAsync()).Should().Be(1);
    }
}
