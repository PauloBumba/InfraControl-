using FluentAssertions;
using InfraControl.Application.Identity.Invitations.Queries;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InfraControl.UnitTests.Identity;

/// <summary>
/// ADR-030 §2 — GET /admin/invitations/{invitationId}: recuperação pontual
/// do convite (Administer, mesma 2ª camada do Search) com o DTO da lista;
/// inexistente = 404 pt-BR.
/// </summary>
public sealed class InvitationQueryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public InvitationQueryTests()
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

    private static async Task<Invitation> SeedInvitationAsync(AppDbContext context)
    {
        var invitation = Invitation.Create(
            "convidado@example.com",
            "hash-do-token",
            DateTime.UtcNow.AddDays(3),
            Guid.NewGuid(),
            [UserRole.Operator, UserRole.Viewer]);
        context.Invitations.Add(invitation);
        await context.SaveChangesAsync();
        return invitation;
    }

    private static GetInvitationQueryHandler Handler(AppDbContext context)
        => new(new InvitationRepository(context));

    [Fact]
    public async Task GetInvitation_Known_ShouldReturnDtoWithRoles()
    {
        await using var context = await CreateContextAsync();
        var invitation = await SeedInvitationAsync(context);

        var dto = await Handler(context).Handle(
            new GetInvitationQuery(invitation.Id), CancellationToken.None);

        dto.Id.Should().Be(invitation.Id);
        dto.Email.Should().Be("convidado@example.com");
        dto.Status.Should().Be(InvitationStatus.Pending);
        dto.Roles.Should().BeEquivalentTo(["Operator", "Viewer"]);
    }

    [Fact]
    public async Task GetInvitation_Unknown_ShouldThrowKeyNotFoundPtBr()
    {
        await using var context = await CreateContextAsync();
        var unknownId = Guid.NewGuid();

        var action = () => Handler(context).Handle(
            new GetInvitationQuery(unknownId), CancellationToken.None);

        (await action.Should().ThrowAsync<KeyNotFoundException>())
            .WithMessage($"Convite '{unknownId}' não encontrado.");
    }
}
