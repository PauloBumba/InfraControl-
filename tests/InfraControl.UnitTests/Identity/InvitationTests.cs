using FluentAssertions;
using InfraControl.Application.Identity.Invitations.Commands;
using InfraControl.Application.Identity.Invitations.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Infrastructure.Services;

namespace InfraControl.UnitTests.Identity;

public sealed class InvitationTests
{
    [Fact]
    public void Create_ShouldNormalizeEmailAndRaiseEvent()
    {
        var invitation = Invitation.Create(
            "New.User@Example.com",
            "hash-123",
            DateTime.UtcNow.AddDays(7),
            Guid.NewGuid(),
            [UserRole.User]);

        invitation.Email.Should().Be("new.user@example.com");
        invitation.Status.Should().Be(InvitationStatus.Pending);
        invitation.Roles.Should().ContainSingle().Which.Should().Be(UserRole.User);
        invitation.DomainEvents.Should().ContainSingle();
    }

    [Fact]
    public void Create_ShouldRejectInvalidEmail()
    {
        var action = () => Invitation.Create(
            "not-an-email",
            "hash-123",
            DateTime.UtcNow.AddDays(7),
            Guid.NewGuid(),
            [UserRole.Viewer]);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldRejectPastExpiry()
    {
        var action = () => Invitation.Create(
            "user@example.com",
            "hash-123",
            DateTime.UtcNow.AddDays(-1),
            Guid.NewGuid(),
            [UserRole.Viewer]);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldRejectEmptyRoles()
    {
        var action = () => Invitation.Create(
            "user@example.com",
            "hash-123",
            DateTime.UtcNow.AddDays(7),
            Guid.NewGuid(),
            []);

        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Cancel_ShouldTransitionPendingToCancelled()
    {
        var invitation = Invitation.Create(
            "user@example.com",
            "hash-123",
            DateTime.UtcNow.AddDays(7),
            Guid.NewGuid(),
            [UserRole.Viewer]);

        invitation.Cancel();

        invitation.Status.Should().Be(InvitationStatus.Cancelled);
        invitation.DecidedAt.Should().NotBeNull();
    }

    [Fact]
    public void Cancel_ShouldRejectNonPending()
    {
        var invitation = Invitation.Create(
            "user@example.com",
            "hash-123",
            DateTime.UtcNow.AddDays(7),
            Guid.NewGuid(),
            [UserRole.Viewer]);
        invitation.Cancel();

        var action = () => invitation.Cancel();

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateHandler_ShouldPersistAndReturnSingleUseToken()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var handler = scope.ServiceProvider.GetRequiredService<
            IRequestHandler<CreateInvitationCommand, CreateInvitationResult>>();
        var inviterId = Guid.NewGuid();

        var result = await handler.Handle(
            new CreateInvitationCommand("invited@example.com", [UserRole.User], inviterId),
            CancellationToken.None);

        result.InvitationId.Should().NotBeEmpty();
        result.Token.Should().NotBeNullOrWhiteSpace();

        var repository = scope.ServiceProvider.GetRequiredService<IInvitationRepository>();
        var stored = await repository.GetAsync(result.InvitationId);
        stored.Should().NotBeNull();
        stored!.Email.Should().Be("invited@example.com");
        stored.Status.Should().Be(InvitationStatus.Pending);
        stored.TokenHash.Should().NotBe(result.Token);
        stored.TokenHash.Should().HaveLength(64);
    }

    [Fact]
    public async Task CreateHandler_ShouldRejectDuplicatePending()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var handler = scope.ServiceProvider.GetRequiredService<
            IRequestHandler<CreateInvitationCommand, CreateInvitationResult>>();
        var inviterId = Guid.NewGuid();

        await handler.Handle(
            new CreateInvitationCommand("dup@example.com", [UserRole.Viewer], inviterId),
            CancellationToken.None);

        var action = () => handler.Handle(
            new CreateInvitationCommand("dup@example.com", [UserRole.Viewer], inviterId),
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void Accept_ShouldTransitionPendingToAccepted()
    {
        var invitation = Invitation.Create(
            "user@example.com",
            "hash-123",
            DateTime.UtcNow.AddDays(7),
            Guid.NewGuid(),
            [UserRole.User]);

        invitation.Accept("hash-123", DateTime.UtcNow);

        invitation.Status.Should().Be(InvitationStatus.Accepted);
        invitation.DecidedAt.Should().NotBeNull();
        invitation.DomainEvents.Should().HaveCount(2);
    }

    [Fact]
    public void Accept_ShouldRejectWrongToken()
    {
        var invitation = Invitation.Create(
            "user@example.com",
            "hash-123",
            DateTime.UtcNow.AddDays(7),
            Guid.NewGuid(),
            [UserRole.Viewer]);

        var action = () => invitation.Accept("wrong-hash", DateTime.UtcNow);

        action.Should().Throw<UnauthorizedAccessException>();
        invitation.Status.Should().Be(InvitationStatus.Pending);
    }

    [Fact]
    public void Accept_ShouldMarkExpiredWhenPastExpiry()
    {
        var invitation = Invitation.Create(
            "user@example.com",
            "hash-123",
            DateTime.UtcNow.AddDays(7),
            Guid.NewGuid(),
            [UserRole.Viewer]);
        var afterExpiry = invitation.ExpiresAt.AddMinutes(1);

        var action = () => invitation.Accept("hash-123", afterExpiry);

        action.Should().Throw<InvalidOperationException>().WithMessage("*expirou*");
        invitation.Status.Should().Be(InvitationStatus.Expired);
    }

    [Fact]
    public async Task AcceptHandler_ShouldAcceptAndEnforceSingleUse()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var createHandler = scope.ServiceProvider.GetRequiredService<
            IRequestHandler<CreateInvitationCommand, CreateInvitationResult>>();
        var acceptHandler = scope.ServiceProvider.GetRequiredService<
            IRequestHandler<AcceptInvitationCommand, AcceptInvitationResult>>();

        var created = await createHandler.Handle(
            new CreateInvitationCommand("accept@example.com", [UserRole.User], Guid.NewGuid()),
            CancellationToken.None);

        var accepted = await acceptHandler.Handle(
            new AcceptInvitationCommand(created.InvitationId, created.Token),
            CancellationToken.None);

        accepted.Email.Should().Be("accept@example.com");
        accepted.Roles.Should().ContainSingle().Which.Should().Be(UserRole.User);

        var repository = scope.ServiceProvider.GetRequiredService<IInvitationRepository>();
        (await repository.GetAsync(created.InvitationId))!.Status
            .Should().Be(InvitationStatus.Accepted);

        var secondAccept = () => acceptHandler.Handle(
            new AcceptInvitationCommand(created.InvitationId, created.Token),
            CancellationToken.None);

        await secondAccept.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AcceptHandler_ShouldRejectUnknownInvitation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var acceptHandler = scope.ServiceProvider.GetRequiredService<
            IRequestHandler<AcceptInvitationCommand, AcceptInvitationResult>>();

        var action = () => acceptHandler.Handle(
            new AcceptInvitationCommand(Guid.NewGuid(), "any-token"),
            CancellationToken.None);

        await action.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public void Reject_ShouldTransitionPendingToRejected()
    {
        var invitation = Invitation.Create(
            "user@example.com",
            "hash-123",
            DateTime.UtcNow.AddDays(7),
            Guid.NewGuid(),
            [UserRole.Viewer]);

        invitation.Reject("hash-123", DateTime.UtcNow);

        invitation.Status.Should().Be(InvitationStatus.Rejected);
        invitation.DecidedAt.Should().NotBeNull();
        invitation.DomainEvents.Should().HaveCount(2);
    }

    [Fact]
    public void Reject_ShouldRejectWrongToken()
    {
        var invitation = Invitation.Create(
            "user@example.com",
            "hash-123",
            DateTime.UtcNow.AddDays(7),
            Guid.NewGuid(),
            [UserRole.Viewer]);

        var action = () => invitation.Reject("wrong-hash", DateTime.UtcNow);

        action.Should().Throw<UnauthorizedAccessException>();
        invitation.Status.Should().Be(InvitationStatus.Pending);
    }

    [Fact]
    public async Task RejectHandler_ShouldRejectAndBlockReuse()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var createHandler = scope.ServiceProvider.GetRequiredService<
            IRequestHandler<CreateInvitationCommand, CreateInvitationResult>>();
        var rejectHandler = scope.ServiceProvider.GetRequiredService<
            IRequestHandler<RejectInvitationCommand>>();

        var created = await createHandler.Handle(
            new CreateInvitationCommand("reject@example.com", [UserRole.Viewer], Guid.NewGuid()),
            CancellationToken.None);

        await rejectHandler.Handle(
            new RejectInvitationCommand(created.InvitationId, created.Token),
            CancellationToken.None);

        var repository = scope.ServiceProvider.GetRequiredService<IInvitationRepository>();
        (await repository.GetAsync(created.InvitationId))!.Status
            .Should().Be(InvitationStatus.Rejected);

        var reuse = () => rejectHandler.Handle(
            new RejectInvitationCommand(created.InvitationId, created.Token),
            CancellationToken.None);

        await reuse.Should().ThrowAsync<InvalidOperationException>();
    }

    private static ServiceProvider BuildProvider(SqliteConnection connection)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(typeof(CreateInvitationCommand).Assembly));
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
        services.AddScoped<IDomainEventService, DomainEventService>();
        services.AddScoped<IInvitationRepository, InvitationRepository>();
        services.AddScoped<IInvitationAccountPort, NoOpInvitationAccountPort>();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Porta registrada apenas para os testes de ciclo de vida do convite,
    /// que validam o agregado sem provisionar conta real.
    /// </summary>
    private sealed class NoOpInvitationAccountPort : IInvitationAccountPort
    {
        public Task<Guid> ProvisionInactiveAccountAsync(
            string email,
            IReadOnlyCollection<Domain.Enums.UserRole> roles,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Guid.NewGuid());

        public Task<Application.Identity.Authentication.OperationResult> SetPasswordAndActivateAsync(
            string email,
            string password,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Application.Identity.Authentication.OperationResult.Success());
    }
}
