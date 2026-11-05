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
/// F48-T002: projeto concluído (NewStatus=Completed) sinaliza os membros
/// do projeto (decisão do usuário 2026-10-29) — deduplicados; transições
/// para outros status não geram notificação.
/// </summary>
public sealed class ProjectCompletedNotificationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public ProjectCompletedNotificationTests()
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

    private static async Task<Guid> SeedUserAsync(AppDbContext context, Guid? userId = null)
    {
        var id = userId ?? Guid.NewGuid();
        context.DomainUsers.Add(User.Create(
            id, $"user-{id}@exemplo.test", "Usuário F48", UserRole.Operator));
        await context.SaveChangesAsync();
        return id;
    }

    private static Mock<ConsumeContext<ProjectStatusChangedDomainEvent>> ContextFor(
        ProjectStatusChangedDomainEvent domainEvent)
    {
        var context = new Mock<ConsumeContext<ProjectStatusChangedDomainEvent>>();
        context.SetupGet(c => c.Message).Returns(domainEvent);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return context;
    }

    [Fact]
    public async Task Consume_WhenCompleted_NotifiesProjectMembersAndLeader()
    {
        var (context, consumer) = await CreateAsync();
        var leader = await SeedUserAsync(context);
        var memberA = await SeedUserAsync(context);
        var memberB = await SeedUserAsync(context);

        var project = Project.Create("PRJ-001", "Rede nova", "Troca de switches", leader);
        context.Projects.Add(project);
        context.ProjectMembers.Add(ProjectMember.Create(project.Id, memberA, ProjectMemberRole.Member));
        context.ProjectMembers.Add(ProjectMember.Create(project.Id, memberB, ProjectMemberRole.Reviewer));
        await context.SaveChangesAsync();

        var evt = new ProjectStatusChangedDomainEvent(
            project.Id, ProjectStatus.InProgress, ProjectStatus.Completed, "entregas finalizadas");
        await consumer.Consume(ContextFor(evt).Object);

        var notification = await context.Notifications.AsNoTracking().SingleAsync();
        notification.EventType.Should().Be(nameof(ProjectStatusChangedDomainEvent));
        notification.Title.Should().Be("Projeto concluído");

        var recipients = await context.NotificationRecipients.AsNoTracking().ToListAsync();
        recipients.Select(r => r.UserId)
            .Should().BeEquivalentTo(new[] { leader, memberA, memberB });
    }

    [Fact]
    public async Task Consume_WhenNotCompleted_DoesNotNotify()
    {
        var (context, consumer) = await CreateAsync();
        var leader = await SeedUserAsync(context);
        var project = Project.Create("PRJ-002", "Obras", "Escopo", leader);
        context.Projects.Add(project);
        await context.SaveChangesAsync();

        var evt = new ProjectStatusChangedDomainEvent(
            project.Id, ProjectStatus.Draft, ProjectStatus.InProgress, "kickoff");
        await consumer.Consume(ContextFor(evt).Object);

        var notifications = await context.Notifications.AsNoTracking().ToListAsync();
        notifications.Should().BeEmpty();
    }

    [Fact]
    public async Task Consume_DedupsLeaderWhoIsAlsoMember()
    {
        var (context, consumer) = await CreateAsync();
        var leader = await SeedUserAsync(context);
        var project = Project.Create("PRJ-003", "Datacenter", "Migração", leader);
        context.Projects.Add(project);
        context.ProjectMembers.Add(ProjectMember.Create(project.Id, leader, ProjectMemberRole.Leader));
        await context.SaveChangesAsync();

        var evt = new ProjectStatusChangedDomainEvent(
            project.Id, ProjectStatus.InProgress, ProjectStatus.Completed, null);
        await consumer.Consume(ContextFor(evt).Object);

        var recipients = await context.NotificationRecipients.AsNoTracking().ToListAsync();
        recipients.Should().ContainSingle(r => r.UserId == leader);
    }

    [Fact]
    public async Task Consume_WithoutProjectRow_DoesNotThrow()
    {
        var (context, consumer) = await CreateAsync();

        var evt = new ProjectStatusChangedDomainEvent(
            Guid.NewGuid(), ProjectStatus.InProgress, ProjectStatus.Completed, null);
        var act = async () => await consumer.Consume(ContextFor(evt).Object);

        await act.Should().NotThrowAsync();
        var notifications = await context.Notifications.AsNoTracking().ToListAsync();
        notifications.Should().BeEmpty();
    }
}
