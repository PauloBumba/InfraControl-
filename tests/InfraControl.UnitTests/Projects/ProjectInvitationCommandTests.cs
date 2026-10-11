using FluentAssertions;
using InfraControl.Application.Identity.Authentication;
using InfraControl.Application.Identity.Invitations.Ports;
using InfraControl.Application.Projects.Commands;
using InfraControl.Application.Projects.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Projects;

public sealed class ProjectInvitationCommandTests
{
    [Fact]
    public async Task Create_ShouldSendOnboardingInvitationAndAssignViewerToNewAccount()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var creatorId = Guid.NewGuid();
        var project = Project.Create(
            "PRJ-901", "Projeto de rede", "Migração de rede", creatorId, createdById: creatorId);
        context.Projects.Add(project);
        await context.SaveChangesAsync();

        var accountPort = new FakeInvitationAccountPort(null);
        var emailSender = new RecordingEmailSender();
        var handler = CreateHandler(context, accountPort, emailSender, new FakeCurrentUser(creatorId));

        var result = await handler.Handle(
            new CreateProjectInvitationCommand(
                project.Id,
                "NEW.USER@example.com",
                ProjectMemberRole.Reviewer,
                "https://portal.example/"),
            CancellationToken.None);

        result.AccountExists.Should().BeFalse();
        result.RequiresActivation.Should().BeTrue();
        var invitation = await new InvitationRepository(context).GetAsync(result.InvitationId);
        invitation.Should().NotBeNull();
        invitation!.Email.Should().Be("new.user@example.com");
        invitation.ProjectId.Should().Be(project.Id);
        invitation.ProjectRole.Should().Be(ProjectMemberRole.Reviewer);
        invitation.Roles.Should().ContainSingle().Which.Should().Be(UserRole.Viewer);

        var sent = emailSender.Invitations.Should().ContainSingle().Which;
        sent.To.Should().Be("new.user@example.com");
        sent.Url.Should().StartWith($"https://portal.example/invitations/{result.InvitationId}?token=");
    }

    [Fact]
    public async Task Create_ShouldSendLoginLinkForActiveAccount()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var creatorId = Guid.NewGuid();
        var project = Project.Create(
            "PRJ-902", "Projeto de datacenter", "Modernização", creatorId, createdById: creatorId);
        context.Projects.Add(project);
        await context.SaveChangesAsync();

        var account = new InvitationAccountInfo(Guid.NewGuid(), IsActive: true);
        var emailSender = new RecordingEmailSender();
        var handler = CreateHandler(
            context,
            new FakeInvitationAccountPort(account),
            emailSender,
            new FakeCurrentUser(creatorId));

        var result = await handler.Handle(
            new CreateProjectInvitationCommand(
                project.Id,
                "existing@example.com",
                ProjectMemberRole.Member,
                "https://portal.example"),
            CancellationToken.None);

        result.AccountExists.Should().BeTrue();
        result.RequiresActivation.Should().BeFalse();
        emailSender.Invitations.Should().ContainSingle()
            .Which.Url.Should().Be("https://portal.example/invitations");
    }

    [Fact]
    public async Task Create_ShouldAllowSameEmailAcrossProjectsButRejectDuplicateWithinProject()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var creatorId = Guid.NewGuid();
        var firstProject = Project.Create(
            "PRJ-904", "Projeto 1", "Descrição", creatorId, createdById: creatorId);
        var secondProject = Project.Create(
            "PRJ-905", "Projeto 2", "Descrição", creatorId, createdById: creatorId);
        context.Projects.AddRange(firstProject, secondProject);
        await context.SaveChangesAsync();

        var emailSender = new RecordingEmailSender();
        var handler = CreateHandler(
            context,
            new FakeInvitationAccountPort(null),
            emailSender,
            new FakeCurrentUser(creatorId));

        await handler.Handle(
            new CreateProjectInvitationCommand(firstProject.Id, "team@example.com", ProjectMemberRole.Member),
            CancellationToken.None);
        await handler.Handle(
            new CreateProjectInvitationCommand(secondProject.Id, "team@example.com", ProjectMemberRole.Reviewer),
            CancellationToken.None);

        var duplicate = () => handler.Handle(
            new CreateProjectInvitationCommand(firstProject.Id, "team@example.com", ProjectMemberRole.Leader),
            CancellationToken.None);
        await duplicate.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*neste projeto*");

        emailSender.Invitations.Should().HaveCount(2);
        (await context.Invitations.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Create_ShouldRejectNonCreatorWhoIsNotSystemAdmin()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var project = Project.Create(
            "PRJ-903", "Projeto protegido", "Descrição", Guid.NewGuid(), createdById: Guid.NewGuid());
        context.Projects.Add(project);
        await context.SaveChangesAsync();

        var emailSender = new RecordingEmailSender();
        var handler = CreateHandler(
            context,
            new FakeInvitationAccountPort(null),
            emailSender,
            new FakeCurrentUser(Guid.NewGuid()));

        var action = () => handler.Handle(
            new CreateProjectInvitationCommand(
                project.Id,
                "user@example.com",
                ProjectMemberRole.Member),
            CancellationToken.None);

        await action.Should().ThrowAsync<UnauthorizedAccessException>();
        emailSender.Invitations.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_ShouldAllowSystemAdminToInviteToAnyProject()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var project = Project.Create(
            "PRJ-906", "Projeto administrado", "Descrição", Guid.NewGuid(), createdById: Guid.NewGuid());
        context.Projects.Add(project);
        await context.SaveChangesAsync();

        var emailSender = new RecordingEmailSender();
        var handler = CreateHandler(
            context,
            new FakeInvitationAccountPort(null),
            emailSender,
            new FakeCurrentUser(Guid.NewGuid(), UserRole.Admin.ToString()));

        var result = await handler.Handle(
            new CreateProjectInvitationCommand(
                project.Id,
                "admin-invite@example.com",
                ProjectMemberRole.Leader),
            CancellationToken.None);

        result.InvitationId.Should().NotBeEmpty();
        emailSender.Invitations.Should().ContainSingle();
    }

    private static CreateProjectInvitationCommandHandler CreateHandler(
        AppDbContext context,
        IInvitationAccountPort accountPort,
        IIdentityEmailSender emailSender,
        ICurrentUserContext currentUser)
        => new(
            new ProjectRepository(context),
            new InvitationRepository(context),
            accountPort,
            emailSender,
            currentUser,
            NullLogger<CreateProjectInvitationCommandHandler>.Instance);

    private sealed class FakeCurrentUser(Guid userId, params string[] roles) : ICurrentUserContext
    {
        public Guid? UserId => userId;
        public string? UserName => "inviter@example.com";
        public bool IsAuthenticated => true;
        public IReadOnlyCollection<string> Roles => roles;
    }

    private sealed class FakeInvitationAccountPort(InvitationAccountInfo? account) : IInvitationAccountPort
    {
        public Task<InvitationAccountInfo?> GetAccountInfoAsync(
            string email,
            CancellationToken cancellationToken = default)
            => Task.FromResult(account);

        public Task<Guid> ProvisionInactiveAccountAsync(
            string email,
            IReadOnlyCollection<UserRole> roles,
            CancellationToken cancellationToken = default)
            => Task.FromResult(account?.UserId ?? Guid.NewGuid());

        public Task<OperationResult> SetPasswordAndActivateAsync(
            string email,
            string password,
            CancellationToken cancellationToken = default)
            => Task.FromResult(OperationResult.Success());
    }

    private sealed class RecordingEmailSender : IIdentityEmailSender
    {
        public List<(string To, string Url)> Invitations { get; } = [];

        public Task SendPasswordResetAsync(
            string email,
            string resetUrl,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SendInvitationAsync(
            string email,
            string acceptUrl,
            CancellationToken cancellationToken = default)
        {
            Invitations.Add((email, acceptUrl));
            return Task.CompletedTask;
        }
    }
}
