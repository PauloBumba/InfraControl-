using FluentAssertions;
using InfraControl.Application.Identity.Authentication;
using InfraControl.Application.Identity.Invitations.Commands;
using InfraControl.Application.Identity.Invitations.Ports;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InfraControl.UnitTests.Identity;

/// <summary>
/// Reenvio de convite pendente: rotaciona o token (link anterior morre),
/// reenvia e-mail só quando pedido e devolve o AcceptUrl para copiar.
/// </summary>
public sealed class InvitationResendCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly FakeEmailSender _emailSender = new();

    public InvitationResendCommandTests()
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

    private ResendInvitationCommandHandler Handler(AppDbContext context)
        => new(
            new InvitationRepository(context),
            _emailSender,
            NullLogger<ResendInvitationCommandHandler>.Instance);

    private static async Task<Guid> SeedPendingAsync(AppDbContext context)
    {
        var invitation = Invitation.Create(
            "convidado@exemplo.test",
            "hash-" + Guid.NewGuid().ToString("N"),
            DateTime.UtcNow.AddDays(7),
            Guid.NewGuid(),
            [UserRole.Viewer]);
        context.Invitations.Add(invitation);
        await context.SaveChangesAsync();
        return invitation.Id;
    }

    [Fact]
    public void Command_ShouldRequireAdminister()
    {
        var command = new ResendInvitationCommand(Guid.NewGuid());
        command.RequiredRoles.Should().Contain(RoleAccess.Administer);
    }

    [Fact]
    public async Task Handle_ShouldRotateTokenAndSendEmail()
    {
        await using var context = await CreateContextAsync();
        var id = await SeedPendingAsync(context);
        var originalHash = (await context.Invitations.FindAsync(id))!.TokenHash;

        var result = await Handler(context).Handle(
            new ResendInvitationCommand(id, SendEmail: true, BaseUrl: "https://app.exemplo"),
            CancellationToken.None);

        result.AcceptUrl.Should().StartWith($"https://app.exemplo/invitations/{id}?token=");
        var stored = await context.Invitations.FindAsync(id);
        stored!.TokenHash.Should().NotBe(originalHash);
        _emailSender.LastUrl.Should().Be(result.AcceptUrl);
        _emailSender.LastEmail.Should().Be("convidado@exemplo.test");
    }

    [Fact]
    public async Task Handle_ShouldReturnLinkWithoutSendingEmail_WhenSendEmailFalse()
    {
        await using var context = await CreateContextAsync();
        var id = await SeedPendingAsync(context);

        var result = await Handler(context).Handle(
            new ResendInvitationCommand(id, SendEmail: false, BaseUrl: "https://app.exemplo"),
            CancellationToken.None);

        result.AcceptUrl.Should().Contain($"/invitations/{id}?token=");
        _emailSender.SendCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldInvalidateOldToken()
    {
        await using var context = await CreateContextAsync();
        var id = await SeedPendingAsync(context);
        var handler = Handler(context);

        var first = await handler.Handle(
            new ResendInvitationCommand(id, SendEmail: false, BaseUrl: "https://app.exemplo"),
            CancellationToken.None);
        var second = await handler.Handle(
            new ResendInvitationCommand(id, SendEmail: false, BaseUrl: "https://app.exemplo"),
            CancellationToken.None);

        var firstToken = first.AcceptUrl.Split("token=")[1];
        var secondToken = second.AcceptUrl.Split("token=")[1];
        firstToken.Should().NotBe(secondToken);

        var storedHash = InvitationToken.Hash(firstToken);
        (await context.Invitations.FindAsync(id))!.TokenHash.Should().NotBe(storedHash);
        InvitationToken.Hash(secondToken).Should().Be((await context.Invitations.FindAsync(id))!.TokenHash);
    }

    [Fact]
    public async Task Handle_ShouldRejectNonPending()
    {
        await using var context = await CreateContextAsync();
        var invitation = Invitation.Create(
            "x@exemplo.test",
            "hash-" + Guid.NewGuid().ToString("N"),
            DateTime.UtcNow.AddDays(7),
            Guid.NewGuid(),
            [UserRole.Viewer]);
        invitation.Cancel();
        context.Invitations.Add(invitation);
        await context.SaveChangesAsync();

        var act = () => Handler(context).Handle(
            new ResendInvitationCommand(invitation.Id, SendEmail: false, BaseUrl: "https://x"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private sealed class FakeEmailSender : IIdentityEmailSender
    {
        public int SendCount { get; private set; }
        public string? LastEmail { get; private set; }
        public string? LastUrl { get; private set; }

        public Task SendInvitationAsync(string email, string acceptUrl, CancellationToken cancellationToken = default)
        {
            SendCount++;
            LastEmail = email;
            LastUrl = acceptUrl;
            return Task.CompletedTask;
        }

        public Task SendPasswordResetAsync(string email, string resetUrl, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
