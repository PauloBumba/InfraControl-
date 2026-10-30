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
/// F48-T003: PDI concluído (Status=Completed) muda a sinalização para
/// "PDI concluído" — dono do plano é o destinatário (decisão do usuário
/// 2026-10-29); demais status mantêm "PDI atualizado".
/// </summary>
public sealed class PdiCompletedNotificationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public PdiCompletedNotificationTests()
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

    private static async Task<Guid> SeedUserAsync(AppDbContext context)
    {
        var id = Guid.NewGuid();
        context.DomainUsers.Add(User.Create(
            id, $"user-{id}@exemplo.test", "Usuário F48", UserRole.Operator));
        await context.SaveChangesAsync();
        return id;
    }

    private static Mock<ConsumeContext<PdiUpdatedDomainEvent>> ContextFor(
        PdiUpdatedDomainEvent domainEvent)
    {
        var context = new Mock<ConsumeContext<PdiUpdatedDomainEvent>>();
        context.SetupGet(c => c.Message).Returns(domainEvent);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return context;
    }

    [Fact]
    public async Task Consume_WhenCompleted_TitlesPdiConcluidoAndNotifiesOwner()
    {
        var (context, consumer) = await CreateAsync();
        var owner = await SeedUserAsync(context);
        var actor = await SeedUserAsync(context);

        var evt = new PdiUpdatedDomainEvent(
            owner, new DateOnly(2026, 12, 31), PdiStatus.Completed,
            actor, "ator@exemplo.test", "dono@exemplo.test");
        await consumer.Consume(ContextFor(evt).Object);

        var notification = await context.Notifications.AsNoTracking().SingleAsync();
        notification.Title.Should().Be("PDI concluído");
        notification.Message.Should().Contain("dono@exemplo.test");

        var recipient = await context.NotificationRecipients.AsNoTracking().SingleAsync();
        recipient.UserId.Should().Be(owner);
    }

    [Fact]
    public async Task Consume_WhenNotCompleted_KeepsPdiAtualizado()
    {
        var (context, consumer) = await CreateAsync();
        var owner = await SeedUserAsync(context);
        var actor = await SeedUserAsync(context);

        var evt = new PdiUpdatedDomainEvent(
            owner, new DateOnly(2026, 12, 31), PdiStatus.InProgress,
            actor, "ator@exemplo.test", "dono@exemplo.test");
        await consumer.Consume(ContextFor(evt).Object);

        var notification = await context.Notifications.AsNoTracking().SingleAsync();
        notification.Title.Should().Be("PDI atualizado");
    }

    [Fact]
    public async Task Consume_WhenCompletedByOwner_NotifiesAdminRole()
    {
        var (context, consumer) = await CreateAsync();
        var owner = await SeedUserAsync(context);

        var evt = new PdiUpdatedDomainEvent(
            owner, new DateOnly(2026, 12, 31), PdiStatus.Completed,
            owner, "dono@exemplo.test", "dono@exemplo.test");
        await consumer.Consume(ContextFor(evt).Object);

        var notification = await context.Notifications.AsNoTracking().SingleAsync();
        notification.Title.Should().Be("PDI concluído");

        var recipient = await context.NotificationRecipients.AsNoTracking().SingleAsync();
        recipient.Role.Should().Be(UserRole.Admin.ToString());
        recipient.UserId.Should().BeNull();
    }
}
