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
/// F48-T001: task concluída (Resolved/Closed) sinaliza de volta para o
/// executor (quem resolveu), o solicitante (CreatedById) e o responsável
/// (ResponsibleId) — destinatários por usuário, deduplicados.
/// </summary>
public sealed class TaskResolvedNotificationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public TaskResolvedNotificationTests()
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

    private static async Task<TaskItem> SeedTaskAsync(
        AppDbContext context, Guid? createdById, Guid? responsibleId)
    {
        // destinatários por usuário — FK de NotificationDelivery exige a linha.
        foreach (var userId in new[] { createdById, responsibleId }.OfType<Guid>().Distinct())
            context.DomainUsers.Add(User.Create(
                userId, $"user-{userId}@exemplo.test", "Usuário F48", UserRole.Operator));

        var task = TaskItem.Create(
            "Trocar switch do rack A", TaskKind.Manual, createdById: createdById);
        if (responsibleId is { } responsible)
            task.SetResponsible(responsible);
        context.Tasks.Add(task);
        await context.SaveChangesAsync();
        return task;
    }

    private static async Task SeedUserAsync(AppDbContext context, Guid userId)
        => context.DomainUsers.Add(User.Create(
            userId, $"user-{userId}@exemplo.test", "Usuário F48", UserRole.Operator));

    private static Mock<ConsumeContext<TaskResolvedDomainEvent>> ContextFor(
        TaskResolvedDomainEvent domainEvent)
    {
        var context = new Mock<ConsumeContext<TaskResolvedDomainEvent>>();
        context.SetupGet(c => c.Message).Returns(domainEvent);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return context;
    }

    [Fact]
    public async Task Consume_NotifiesExecutorCreatorAndResponsible()
    {
        var (context, consumer) = await CreateAsync();
        var creator = Guid.NewGuid();
        var responsible = Guid.NewGuid();
        var executor = Guid.NewGuid();
        await SeedUserAsync(context, executor);
        var task = await SeedTaskAsync(context, creator, responsible);

        var evt = new TaskResolvedDomainEvent(
            task.Id, task.Title, TaskState.Resolved, executor);
        await consumer.Consume(ContextFor(evt).Object);

        var notification = await context.Notifications.AsNoTracking().SingleAsync();
        notification.EventType.Should().Be(nameof(TaskResolvedDomainEvent));
        notification.Title.Should().Be("Task concluída");
        notification.Message.Should().Contain("Trocar switch do rack A");

        var recipients = await context.NotificationRecipients.AsNoTracking().ToListAsync();
        recipients.Select(r => r.UserId)
            .Should().BeEquivalentTo(new[] { executor, creator, responsible });
        recipients.Should().OnlyContain(r => r.UserId != null);
    }

    [Fact]
    public async Task Consume_DedupsWhenExecutorIsAlsoCreator()
    {
        var (context, consumer) = await CreateAsync();
        var same = Guid.NewGuid();
        var task = await SeedTaskAsync(context, createdById: same, responsibleId: null);

        var evt = new TaskResolvedDomainEvent(task.Id, task.Title, TaskState.Closed, same);
        await consumer.Consume(ContextFor(evt).Object);

        var recipients = await context.NotificationRecipients.AsNoTracking().ToListAsync();
        recipients.Should().ContainSingle(r => r.UserId == same);
    }

    [Fact]
    public async Task Consume_WithoutTaskRow_NotifyOnlyExecutor()
    {
        var (context, consumer) = await CreateAsync();
        var executor = Guid.NewGuid();
        await SeedUserAsync(context, executor);

        var evt = new TaskResolvedDomainEvent(
            Guid.NewGuid(), "órfã", TaskState.Resolved, executor);
        await consumer.Consume(ContextFor(evt).Object);

        var recipients = await context.NotificationRecipients.AsNoTracking().ToListAsync();
        recipients.Should().ContainSingle(r => r.UserId == executor);
    }

    [Fact]
    public async Task Consume_IsIdempotentViaInbox()
    {
        var (context, consumer) = await CreateAsync();
        var executor = Guid.NewGuid();
        await SeedUserAsync(context, executor);
        var task = await SeedTaskAsync(context, createdById: null, responsibleId: null);
        var evt = new TaskResolvedDomainEvent(task.Id, task.Title, TaskState.Resolved, executor);

        await consumer.Consume(ContextFor(evt).Object);
        await consumer.Consume(ContextFor(evt).Object);

        var notifications = await context.Notifications.AsNoTracking().ToListAsync();
        notifications.Should().ContainSingle();
    }
}
