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

    private static ServiceProvider BuildProvider(SqliteConnection connection)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(typeof(CreateInvitationCommand).Assembly));
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
        services.AddScoped<IInvitationRepository, InvitationRepository>();
        return services.BuildServiceProvider();
    }
}
