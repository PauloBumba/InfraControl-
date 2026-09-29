using FluentAssertions;
using InfraControl.Application.Common.Behaviors;
using InfraControl.Application.Common.Interfaces;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Infrastructure;

/// <summary>
/// F11-T005: audit com ator (UserName) e resource explícito via
/// <see cref="IAuditableRequest"/> — entrada genérica continua
/// "ApplicationRequest" para os demais requests.
/// </summary>
public sealed class AuditBehaviorTests
{
    [Fact]
    public async Task Handle_ShouldWriteSucceededEntryWithActorUserName()
    {
        var writer = new FakeAuditWriter();
        var behavior = CreateBehavior<TestCommand>(writer);

        var result = await behavior.Handle(
            new TestCommand(),
            () => Task.FromResult("ok"),
            CancellationToken.None);

        result.Should().Be("ok");
        var entry = writer.Entries.Should().ContainSingle().Subject;
        entry.Action.Should().Be(nameof(TestCommand));
        entry.Resource.Should().Be("ApplicationRequest");
        entry.Outcome.Should().Be("Succeeded");
        entry.ActorId.Should().Be("test@example.com");
        entry.Error.Should().BeNull();
        entry.CorrelationId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithAuditableRequest_ShouldUseAuditResource()
    {
        var writer = new FakeAuditWriter();
        var behavior = CreateBehavior<TestRemoteRequest>(writer);

        await behavior.Handle(
            new TestRemoteRequest(),
            () => Task.FromResult("ok"),
            CancellationToken.None);

        var entry = writer.Entries.Should().ContainSingle().Subject;
        entry.Resource.Should().Be("RemoteSession");
        entry.Action.Should().Be(nameof(TestRemoteRequest));
        entry.ActorId.Should().Be("test@example.com");
    }

    [Fact]
    public async Task Handle_WhenHandlerThrows_ShouldWriteFailedEntryAndRethrow()
    {
        var writer = new FakeAuditWriter();
        var behavior = CreateBehavior<TestRemoteRequest>(writer);

        var action = () => behavior.Handle(
            new TestRemoteRequest(),
            () => Task.FromException<string>(
                new InvalidOperationException("Falha ao abrir sessão.")),
            CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Falha ao abrir sessão.");
        var entry = writer.Entries.Should().ContainSingle().Subject;
        entry.Outcome.Should().Be("Failed");
        entry.Resource.Should().Be("RemoteSession");
        entry.ActorId.Should().Be("test@example.com");
        entry.Error.Should().Be(nameof(InvalidOperationException));
    }

    private static AuditBehavior<TRequest, string> CreateBehavior<TRequest>(
        IAuditWriter writer)
        where TRequest : notnull
        => new(
            writer,
            new FakeUnitOfWork(),
            new FakeCurrentUser(),
            NullLogger<AuditBehavior<TRequest, string>>.Instance);

    private sealed record TestCommand : ICommand<string>;

    private sealed record TestRemoteRequest : ICommand<string>, IAuditableRequest
    {
        public string AuditResource => "RemoteSession";
    }

    private sealed class FakeAuditWriter : IAuditWriter
    {
        public List<AuditEntry> Entries { get; } = [];

        public Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(1);

        public void Dispose() { }
    }

    private sealed class FakeCurrentUser : ICurrentUserContext
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public string? UserName => "test@example.com";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles => ["Admin"];
    }
}
