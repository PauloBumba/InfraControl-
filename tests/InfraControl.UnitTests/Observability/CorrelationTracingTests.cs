using FluentAssertions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Domain.Events;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Services;
using MassTransit;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace InfraControl.UnitTests.Observability;

/// <summary>
/// ADR-021: o rastro da requisição não pode morrer na API — o id é gravado no
/// outbox quando o evento nasce e vira header da mensagem quando o worker
/// publica no broker, para o consumidor logar sob o mesmo CorrelationId.
/// </summary>
public sealed class CorrelationTracingTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public CorrelationTracingTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
    }

    public void Dispose() => _connection.Dispose();

    /// <summary>Mesmo nome do header gera o rastro legível no Seq/console.</summary>
    private const string ExpectedCorrelation = "corr-42";

    private sealed class FakeCorrelation : ICurrentCorrelation
    {
        public string? Id => ExpectedCorrelation;
    }

    private async Task<AppDbContext> CreateContextAsync()
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    [Fact]
    public async Task PublishAsync_WithActiveCorrelation_ShouldPersistItOnOutbox()
    {
        using var context = await CreateContextAsync();
        var service = new DomainEventService(
            Mock.Of<IPublisher>(),
            context,
            new FakeCorrelation());

        var domainEvent = new UserCreatedDomainEvent(Guid.NewGuid(), "user@example.com");
        await service.PublishAsync(domainEvent, CancellationToken.None);
        await context.SaveChangesAsync();

        var outbox = await context.OutboxMessages.AsNoTracking().SingleAsync();
        outbox.CorrelationId.Should().Be(ExpectedCorrelation);
        outbox.EventType.Should().Be(nameof(UserCreatedDomainEvent));
    }

    [Fact]
    public async Task PublishAsync_WithoutCorrelation_ShouldKeepOutboxNull()
    {
        using var context = await CreateContextAsync();
        var service = new DomainEventService(Mock.Of<IPublisher>(), context);

        var domainEvent = new UserCreatedDomainEvent(Guid.NewGuid(), "user@example.com");
        await service.PublishAsync(domainEvent, CancellationToken.None);
        await context.SaveChangesAsync();

        var outbox = await context.OutboxMessages.AsNoTracking().SingleAsync();
        outbox.CorrelationId.Should().BeNull();
    }

    /// <summary>
    /// Prova de ponta a ponta: o header "CorrelationId" escrito pelo
    /// MassTransitEventBus chega ao consumidor. É exatamente o que o
    /// CorrelationIdConsumeObserver lê nos workers.
    /// </summary>
    [Fact]
    public async Task MassTransitEventBus_ShouldCarryCorrelationHeader_ToConsumer()
    {
        const string expected = "corr-bus-77";
        var received = new TaskCompletionSource<string?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var bus = Bus.Factory.CreateUsingInMemory(cfg =>
        {
            cfg.ReceiveEndpoint("correlation-tracing-test", endpoint =>
            {
                endpoint.Handler<UserCreatedDomainEvent>(context =>
                {
                    received.TrySetResult(
                        context.Headers.Get<string>(MassTransitEventBus.CorrelationHeader));
                    return Task.CompletedTask;
                });
            });
        });

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await bus.StartAsync(cts.Token);
        try
        {
            var eventBus = new MassTransitEventBus(bus);
            var domainEvent = new UserCreatedDomainEvent(Guid.NewGuid(), "user@example.com");

            await eventBus.PublishAsync(domainEvent, domainEvent.GetType(), cts.Token, expected);

            (await received.Task.WaitAsync(cts.Token)).Should().Be(expected);
        }
        finally
        {
            await bus.StopAsync(cts.Token);
        }
    }

    /// <summary>Sem correlationId, publica igual (caminho legado intacto).</summary>
    [Fact]
    public async Task MassTransitEventBus_WithoutCorrelation_ShouldPublishPlain()
    {
        var received = new TaskCompletionSource<object>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var bus = Bus.Factory.CreateUsingInMemory(cfg =>
        {
            cfg.ReceiveEndpoint("correlation-tracing-plain", endpoint =>
            {
                endpoint.Handler<UserCreatedDomainEvent>(context =>
                {
                    received.TrySetResult(context.Message.UserId);
                    return Task.CompletedTask;
                });
            });
        });

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await bus.StartAsync(cts.Token);
        try
        {
            var eventBus = new MassTransitEventBus(bus);
            var domainEvent = new UserCreatedDomainEvent(Guid.NewGuid(), "user@example.com");

            await eventBus.PublishAsync(domainEvent, domainEvent.GetType(), cts.Token);

            (await received.Task.WaitAsync(cts.Token)).Should().Be(domainEvent.UserId);
        }
        finally
        {
            await bus.StopAsync(cts.Token);
        }
    }
}