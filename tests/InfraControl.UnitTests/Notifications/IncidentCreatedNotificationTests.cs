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
/// F16-T002/ADR-023: IncidentCreatedDomainEvent (webhook Zabbix F16-T001,
/// criação manual F6 e auto-abertura F6) vira notificação com destinatários
/// por ROLE — Operator e Admin (decisão do usuário 2026-09-30) — deliveries
/// resolvidas para os usuários dessas roles e idempotência via inbox.
/// </summary>
public sealed class IncidentCreatedNotificationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public IncidentCreatedNotificationTests()
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

    private static IncidentCreatedDomainEvent IncidentEvent(
        string title = "Carga de CPU alta")
        => new(Guid.NewGuid(), title, IncidentSeverity.Critical, Guid.NewGuid());

    /// <summary>
    /// Consome direto o ConsumeContext (Moq) — só Message e CancellationToken
    /// são usados pelo consumer.
    /// </summary>
    private static Mock<ConsumeContext<IncidentCreatedDomainEvent>> ContextFor(
        IncidentCreatedDomainEvent domainEvent)
    {
        var context = new Mock<ConsumeContext<IncidentCreatedDomainEvent>>();
        context.SetupGet(c => c.Message).Returns(domainEvent);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return context;
    }

    [Fact]
    public async Task Consume_IncidentCreated_ShouldNotifyOperatorAndAdminRoles()
    {
        var (context, consumer) = await CreateAsync();

        await consumer.Consume(ContextFor(IncidentEvent()).Object);

        var notification = await context.Notifications.AsNoTracking().SingleAsync();
        notification.EventType.Should().Be(nameof(IncidentCreatedDomainEvent));
        notification.Title.Should().Be("Incidente aberto");
        notification.Message.Should().Contain("Carga de CPU alta")
            .And.Contain("CRÍTICO"); // severidade em pt-BR (WhatsApp, 2026-10-29)

        var recipients = await context.NotificationRecipients.AsNoTracking().ToListAsync();
        recipients.Select(r => r.Role)
            .Should().BeEquivalentTo(new[] { "Operator", "Admin" });
        recipients.Should().OnlyContain(r => r.UserId == null);
    }

    [Fact]
    public async Task Consume_IncidentCreated_ShouldQueueDeliveriesForRoleUsers()
    {
        var (context, consumer) = await CreateAsync();
        var operatorUserId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        context.DomainUsers.Add(User.Create(
            operatorUserId, "operador@exemplo.test", "Operador", UserRole.Operator));
        context.DomainUsers.Add(User.Create(
            adminUserId, "admin@exemplo.test", "Administrador", UserRole.Admin));
        await context.SaveChangesAsync();

        await consumer.Consume(ContextFor(IncidentEvent()).Object);

        // default Email+SignalR × 2 usuários ativos das roles Operator/Admin
        var deliveries = await context.NotificationDeliveries.AsNoTracking().ToListAsync();
        deliveries.Should().HaveCount(4);
        deliveries.Select(d => d.UserId)
            .Should().BeEquivalentTo(new[]
            {
                operatorUserId, operatorUserId, adminUserId, adminUserId
            });
        deliveries.Should().OnlyContain(d => d.Status == DeliveryStatus.Pending);
    }

    [Fact]
    public async Task Consume_IncidentCreated_SameEventTwice_ShouldNotDuplicate()
    {
        var (context, consumer) = await CreateAsync();
        var consume = ContextFor(IncidentEvent());

        await consumer.Consume(consume.Object);
        await consumer.Consume(consume.Object);

        (await context.Notifications.AsNoTracking().CountAsync()).Should().Be(1);
        (await context.NotificationRecipients.AsNoTracking().CountAsync()).Should().Be(2);
        (await context.ProcessedMessages.AsNoTracking().CountAsync()).Should().Be(1);
    }
}
