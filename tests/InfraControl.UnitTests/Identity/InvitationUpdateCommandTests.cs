using FluentAssertions;
using InfraControl.Application.Identity.Invitations.Commands;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Identity;

/// <summary>
/// ADR-030 §1 — PUT do convite pendente (Administer): troca de roles,
/// 404 pt-BR, 400 (roles vazio) e 409 (convite não está pendente).
/// </summary>
public sealed class InvitationUpdateCommandTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public InvitationUpdateCommandTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection)
            .Options;
    }

    public void Dispose() => _connection.Dispose();

    private async Task<AppDbContext> CreateContextAsync()
    {
        var context = new AppDbContext(_options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static UpdateInvitationCommandHandler Handler(AppDbContext context)
        => new(
            new InvitationRepository(context),
            NullLogger<UpdateInvitationCommandHandler>.Instance);

    private static async Task<Guid> SeedPendingAsync(
        AppDbContext context, params UserRole[] roles)
    {
        var invitation = Invitation.Create(
            "convidado@exemplo.test",
            "hash-" + Guid.NewGuid().ToString("N"),
            DateTime.UtcNow.AddDays(7),
            Guid.NewGuid(),
            roles.Length == 0 ? [UserRole.Viewer] : roles);
        context.Invitations.Add(invitation);
        await context.SaveChangesAsync();
        return invitation.Id;
    }

    [Fact]
    public void Command_ShouldRequireAdminister()
        => new UpdateInvitationCommand(Guid.NewGuid(), [UserRole.Operator])
            .RequiredRoles.Should().BeEquivalentTo(RoleAccess.Administer);

    [Fact]
    public async Task Update_Pending_ShouldReplaceRoles()
    {
        await using var context = await CreateContextAsync();
        var id = await SeedPendingAsync(context, UserRole.Viewer);

        var dto = await Handler(context).Handle(
            new UpdateInvitationCommand(id, [UserRole.Operator, UserRole.Admin]),
            CancellationToken.None);

        dto.Id.Should().Be(id);
        dto.Roles.Should().BeEquivalentTo("Operator", "Admin");

        var saved = await context.Invitations
            .Include(invitation => invitation.RoleAssignments)
            .AsNoTracking()
            .SingleAsync(invitation => invitation.Id == id);
        saved.Roles.Should().BeEquivalentTo([UserRole.Operator, UserRole.Admin]);
    }

    [Fact]
    public async Task Update_UnknownId_ShouldThrowNotFoundMessage()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var action = () => Handler(context).Handle(
            new UpdateInvitationCommand(unknownId, [UserRole.Operator]),
            CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Convite '{unknownId}' não encontrado.");
    }

    [Fact]
    public async Task Update_Accepted_ShouldThrowConflictMessage()
    {
        await using var context = await CreateContextAsync();
        var id = await SeedPendingAsync(context);
        var invitation = await context.Invitations.SingleAsync(i => i.Id == id);
        invitation.AcceptInternal(DateTime.UtcNow);
        await context.SaveChangesAsync();

        var action = () => Handler(context).Handle(
            new UpdateInvitationCommand(id, [UserRole.Operator]),
            CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Convite não está pendente.");
    }

    [Fact]
    public async Task Update_EmptyRoles_ShouldThrowArgumentPtBr()
    {
        await using var context = await CreateContextAsync();
        var id = await SeedPendingAsync(context);

        var action = () => Handler(context).Handle(
            new UpdateInvitationCommand(id, []),
            CancellationToken.None);

        (await action.Should().ThrowAsync<ArgumentException>())
            .WithMessage("Ao menos uma função é obrigatória.*");
    }
}
