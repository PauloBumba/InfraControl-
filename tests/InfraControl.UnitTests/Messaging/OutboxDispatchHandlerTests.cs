using FluentAssertions;
using InfraControl.Application.Common.Events;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Events;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Messaging;

/// <summary>
/// ADR-005/docs/06: o domain event publicado pelo AppDbContext vira
/// OutboxMessage na MESMA transação dos dados de negócio — nunca
/// "DB commit → RabbitMQ" sem garantia.
/// </summary>
public sealed class OutboxDispatchHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public OutboxDispatchHandlerTests()
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

    private OutboxDispatchHandler<TEvent> Handler<TEvent>(AppDbContext context)
        where TEvent : InfraControl.Domain.Common.Events.IDomainEvent
        => new(new DomainEventService(null!, context));

    [Fact]
    public async Task Handle_UserCreated_ShouldQueueOutboxMessage()
    {
        await using var context = await CreateContextAsync();
        var domainEvent = new UserCreatedDomainEvent(Guid.NewGuid(), "user@example.com");

        await Handler<UserCreatedDomainEvent>(context)
            .Handle(domainEvent, CancellationToken.None);

        var queued = context.OutboxMessages.Local.Should().ContainSingle().Subject;
        queued.EventType.Should().Be("UserCreatedDomainEvent");
        queued.Payload.Should().Contain("user@example.com");
        queued.ProcessedAt.Should().BeNull();

        await context.SaveChangesAsync();
        (await context.OutboxMessages.AsNoTracking().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Handle_RoleAssigned_ShouldQueueEventWithRoleInPayload()
    {
        await using var context = await CreateContextAsync();
        var domainEvent = new UserRoleAssignedDomainEvent(Guid.NewGuid(), Domain.Enums.UserRole.Admin);

        await Handler<UserRoleAssignedDomainEvent>(context)
            .Handle(domainEvent, CancellationToken.None);

        var queued = context.OutboxMessages.Local.Single();
        queued.EventType.Should().Be("UserRoleAssignedDomainEvent");
        queued.Payload.Should().Contain("Admin");
        queued.Payload.Should().Contain(domainEvent.EventId.ToString());
    }
}
