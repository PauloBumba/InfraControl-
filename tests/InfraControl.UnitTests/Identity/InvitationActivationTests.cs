using FluentAssertions;
using InfraControl.Application.Identity.Authentication;
using InfraControl.Application.Identity.Invitations.Commands;
using InfraControl.Application.Identity.Invitations.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Auth;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using InfraControl.Application.Common.Interfaces;
using InfraControl.Infrastructure.Services;

namespace InfraControl.UnitTests.Identity;

public sealed class InvitationActivationTests
{
    private const string StrongPassword = "ChangeMe!123456";

    // ── Domínio: ciclo de ativação do convite ─────────────────────────────

    [Fact]
    public void EnsureActivatable_ShouldAcceptAcceptedInvitationWithMatchingToken()
    {
        var invitation = CreateAcceptedInvitation(out var token);

        var action = () => invitation.EnsureActivatable(Hash(token), DateTime.UtcNow);

        action.Should().NotThrow();
        invitation.Status.Should().Be(InvitationStatus.Accepted);
    }

    [Fact]
    public void EnsureActivatable_ShouldRejectPendingInvitation()
    {
        var invitation = Invitation.Create(
            "user@example.com", "hash-123", DateTime.UtcNow.AddDays(7),
            Guid.NewGuid(), new[] { UserRole.Viewer });

        var action = () => invitation.EnsureActivatable("hash-123", DateTime.UtcNow);

        action.Should().Throw<InvalidOperationException>().WithMessage("*Pending*");
    }

    [Fact]
    public void EnsureActivatable_ShouldRejectWrongToken()
    {
        var invitation = CreateAcceptedInvitation(out _);

        var action = () => invitation.EnsureActivatable("wrong-hash", DateTime.UtcNow);

        action.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void EnsureActivatable_ShouldRejectExpiredInvitation()
    {
        var invitation = CreateAcceptedInvitation(out var token, expiresInDays: 1);
        var afterExpiry = invitation.ExpiresAt.AddMinutes(1);

        var action = () => invitation.EnsureActivatable(Hash(token), afterExpiry);

        action.Should().Throw<InvalidOperationException>().WithMessage("*expirou*");
    }

    [Fact]
    public void MarkActivated_ShouldTransitionAcceptedToActivatedAndRaiseEvent()
    {
        var invitation = CreateAcceptedInvitation(out _);
        invitation.ClearDomainEvents();

        invitation.MarkActivated();

        invitation.Status.Should().Be(InvitationStatus.Activated);
        invitation.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<InfraControl.Domain.Events.InvitationActivatedDomainEvent>();
    }

    [Fact]
    public void MarkActivated_ShouldRejectAlreadyActivatedInvitation()
    {
        var invitation = CreateAcceptedInvitation(out _);
        invitation.MarkActivated();

        var action = () => invitation.MarkActivated();

        action.Should().Throw<InvalidOperationException>().WithMessage("*Activated*");
    }

    // ── Accept provisiona conta INATIVA ───────────────────────────────────

    [Fact]
    public async Task AcceptHandler_ShouldProvisionInactiveAccount()
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
            new CreateInvitationCommand("invited-user@example.com", [UserRole.Operator], Guid.NewGuid()),
            CancellationToken.None);

        await acceptHandler.Handle(
            new AcceptInvitationCommand(created.InvitationId, created.Token),
            CancellationToken.None);

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var domainUser = await dbContext.DomainUsers
            .Include(user => user.RoleAssignments)
            .SingleAsync(user => user.Email == "invited-user@example.com");

        domainUser.IsActive.Should().BeFalse(
            "accepting an invitation must create the account inactive, awaiting password definition");

        domainUser.Roles.Should().ContainSingle().Which.Should().Be(UserRole.Operator);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<InfraControlUser>>();
        var identityUser = await userManager.FindByEmailAsync("invited-user@example.com");
        identityUser.Should().NotBeNull();
        (await userManager.HasPasswordAsync(identityUser!)).Should().BeFalse(
            "the password must only be set through the activation step");
    }

    [Fact]
    public async Task RejectHandler_ShouldNotCreateAnyAccount()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var createHandler = scope.ServiceProvider.GetRequiredService<
            IRequestHandler<CreateInvitationCommand, CreateInvitationResult>>();
        var rejectHandler = scope.ServiceProvider.GetRequiredService<IRequestHandler<RejectInvitationCommand>>();

        var created = await createHandler.Handle(
            new CreateInvitationCommand("rejected-user@example.com", [UserRole.Viewer], Guid.NewGuid()),
            CancellationToken.None);

        await rejectHandler.Handle(
            new RejectInvitationCommand(created.InvitationId, created.Token),
            CancellationToken.None);

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await dbContext.DomainUsers.AnyAsync(user => user.Email == "rejected-user@example.com"))
            .Should().BeFalse("rejecting an invitation must not create any account");
    }

    // ── set-password ativa a conta ────────────────────────────────────────

    [Fact]
    public async Task SetPasswordHandler_ShouldSetPasswordAndActivateAccount()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var (invitationId, token) = await AcceptAsync(scope, "activate@example.com");

        var setPasswordHandler = scope.ServiceProvider.GetRequiredService<
            IRequestHandler<SetInvitationPasswordCommand, SetInvitationPasswordResult>>();

        var result = await setPasswordHandler.Handle(
            new SetInvitationPasswordCommand(invitationId, token, StrongPassword),
            CancellationToken.None);

        result.InvitationId.Should().Be(invitationId);
        result.Email.Should().Be("activate@example.com");

        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var domainUser = await dbContext.DomainUsers.SingleAsync(user => user.Email == "activate@example.com");
        domainUser.IsActive.Should().BeTrue(
            "defining the password after accepting must activate the account");

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<InfraControlUser>>();
        var identityUser = await userManager.FindByEmailAsync("activate@example.com");
        (await userManager.CheckPasswordAsync(identityUser!, StrongPassword)).Should().BeTrue();

        var repository = scope.ServiceProvider.GetRequiredService<IInvitationRepository>();
        (await repository.GetAsync(invitationId))!.Status
            .Should().Be(InvitationStatus.Activated);
    }

    [Fact]
    public async Task SetPasswordHandler_ShouldEnforceSingleUse()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var (invitationId, token) = await AcceptAsync(scope, "single-use@example.com");

        var setPasswordHandler = scope.ServiceProvider.GetRequiredService<
            IRequestHandler<SetInvitationPasswordCommand, SetInvitationPasswordResult>>();

        await setPasswordHandler.Handle(
            new SetInvitationPasswordCommand(invitationId, token, StrongPassword),
            CancellationToken.None);

        var secondActivation = () => setPasswordHandler.Handle(
            new SetInvitationPasswordCommand(invitationId, token, "AnotherPass!123"),
            CancellationToken.None);

        (await secondActivation.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*Activated*");
    }

    [Fact]
    public async Task SetPasswordHandler_ShouldRejectNonAcceptedInvitation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var createHandler = scope.ServiceProvider.GetRequiredService<
            IRequestHandler<CreateInvitationCommand, CreateInvitationResult>>();
        var created = await createHandler.Handle(
            new CreateInvitationCommand("pending@example.com", [UserRole.Viewer], Guid.NewGuid()),
            CancellationToken.None);

        var setPasswordHandler = scope.ServiceProvider.GetRequiredService<
            IRequestHandler<SetInvitationPasswordCommand, SetInvitationPasswordResult>>();

        var action = () => setPasswordHandler.Handle(
            new SetInvitationPasswordCommand(created.InvitationId, created.Token, StrongPassword),
            CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*Pending*");
    }

    [Fact]
    public async Task SetPasswordHandler_ShouldRejectWrongToken()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var (invitationId, _) = await AcceptAsync(scope, "wrong-token@example.com");

        var setPasswordHandler = scope.ServiceProvider.GetRequiredService<
            IRequestHandler<SetInvitationPasswordCommand, SetInvitationPasswordResult>>();

        var action = () => setPasswordHandler.Handle(
            new SetInvitationPasswordCommand(invitationId, "not-the-real-token", StrongPassword),
            CancellationToken.None);

        await action.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task SetPasswordHandler_ShouldRejectUnknownInvitation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var provider = BuildProvider(connection);
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();

        var setPasswordHandler = scope.ServiceProvider.GetRequiredService<
            IRequestHandler<SetInvitationPasswordCommand, SetInvitationPasswordResult>>();

        var action = () => setPasswordHandler.Handle(
            new SetInvitationPasswordCommand(Guid.NewGuid(), "any-token", StrongPassword),
            CancellationToken.None);

        await action.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static async Task<(Guid InvitationId, string Token)> AcceptAsync(
        IServiceScope scope, string email)
    {
        var createHandler = scope.ServiceProvider.GetRequiredService<
            IRequestHandler<CreateInvitationCommand, CreateInvitationResult>>();
        var acceptHandler = scope.ServiceProvider.GetRequiredService<
            IRequestHandler<AcceptInvitationCommand, AcceptInvitationResult>>();

        var created = await createHandler.Handle(
            new CreateInvitationCommand(email, [UserRole.Operator], Guid.NewGuid()),
            CancellationToken.None);

        await acceptHandler.Handle(
            new AcceptInvitationCommand(created.InvitationId, created.Token),
            CancellationToken.None);

        return (created.InvitationId, created.Token);
    }

    private static Invitation CreateAcceptedInvitation(out string token, int expiresInDays = 7)
    {
        token = InvitationToken.Generate();
        var invitation = Invitation.Create(
            "user@example.com",
            InvitationToken.Hash(token),
            DateTime.UtcNow.AddDays(expiresInDays),
            Guid.NewGuid(),
            [UserRole.Operator]);

        invitation.Accept(InvitationToken.Hash(token), DateTime.UtcNow);
        invitation.ClearDomainEvents();
        return invitation;
    }

    private static string Hash(string token) => InvitationToken.Hash(token);

    private static ServiceProvider BuildProvider(SqliteConnection connection)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMediatR(configuration =>
            configuration.RegisterServicesFromAssembly(typeof(CreateInvitationCommand).Assembly));
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));
        services.AddIdentityCore<InfraControlUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>();
        services.AddScoped<IDomainEventService, DomainEventService>();
        services.AddScoped<IInvitationRepository, InvitationRepository>();
        services.AddScoped<IInvitationAccountPort, InvitationAccountPort>();
        // F1-T017: o handler de convite dispara o e-mail — a porta precisa estar no harness.
        services.AddScoped<IIdentityEmailSender, NoOpEmailSender>();
        return services.BuildServiceProvider();
    }

    /// <summary>F1-T017: só para o handler resolver; nenhum teste aqui afirma e-mail.</summary>
    private sealed class NoOpEmailSender : IIdentityEmailSender
    {
        public Task SendPasswordResetAsync(string email, string resetUrl, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SendInvitationAsync(string email, string acceptUrl, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
