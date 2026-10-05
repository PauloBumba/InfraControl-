using FluentAssertions;
using InfraControl.Application.Identity.Administration;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using InfraControl.IntegrationTests.Common;
using Microsoft.Extensions.DependencyInjection;
using DomainIdentity = InfraControl.Domain.Entities;

namespace InfraControl.IntegrationTests.Identity;

/// <summary>
/// F1-T018 — integration tests para UpdateUserAsync e DeleteUserAsync do
/// IdentityAdministrationService, usando PostgreSQL real via Testcontainers.
/// </summary>
public sealed class UserManagementIntegrationTests : BaseIntegrationTest
{
    private static async Task<DomainIdentity.User> SeedUserAsync(
        AppDbContext context,
        UserRole role = UserRole.Viewer)
    {
        var user = DomainIdentity.User.Create(
            Guid.NewGuid(),
            $"{role.ToString().ToLowerInvariant()}@example.com",
            "Operador",
            role);
        context.DomainUsers.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task UpdateUser_ShouldPersistChangesInDatabase()
    {
        using var scope = CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IIdentityAdministrationService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = await SeedUserAsync(db, UserRole.Viewer);

        var updated = await service.UpdateUserAsync(
            user.Id,
            "Novo Nome",
            "novo@example.com",
            CancellationToken.None);

        updated.Should().BeTrue();

        var dbUser = await db.DomainUsers.FindAsync(user.Id);
        dbUser.Should().NotBeNull();
        dbUser!.DisplayName.Should().Be("Novo Nome");
        dbUser.Email.Should().Be("novo@example.com");
    }

    [Fact]
    public async Task UpdateUser_WithDuplicateEmail_ShouldThrowInvalidOperationException()
    {
        using var scope = CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IIdentityAdministrationService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user1 = await SeedUserAsync(db, UserRole.Viewer);
        var user2 = await SeedUserAsync(db, UserRole.Admin);

        Func<Task> action = () => service.UpdateUserAsync(
            user2.Id,
            "Admin",
            user1.Email,
            CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Já existe um usuário com este e-mail.");
    }

    [Fact]
    public async Task UpdateUser_UnknownUser_ShouldReturnFalse()
    {
        using var scope = CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IIdentityAdministrationService>();

        var updated = await service.UpdateUserAsync(
            Guid.NewGuid(),
            "Nome",
            "email@example.com",
            CancellationToken.None);

        updated.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteUser_ShouldRevokeSessionsAndRemoveUser()
    {
        using var scope = CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IIdentityAdministrationService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = await SeedUserAsync(db, UserRole.Viewer);
        var session = DomainIdentity.UserSession.Start(
            user.Id,
            "refresh-token",
            TimeSpan.FromHours(1),
            TimeSpan.FromMinutes(30));
        db.UserSessions.Add(session);
        await db.SaveChangesAsync();

        var deleted = await service.DeleteUserAsync(
            user.Id,
            Guid.NewGuid(),
            CancellationToken.None);

        deleted.Should().BeTrue();

        var dbSession = await db.UserSessions.FindAsync(session.Id);
        dbSession.Should().NotBeNull();
        dbSession!.RevokedAt.Should().NotBeNull();

        var dbUser = await db.DomainUsers.FindAsync(user.Id);
        dbUser.Should().BeNull();
    }

    [Fact]
    public async Task DeleteUser_SelfDelete_ShouldThrowInvalidOperationException()
    {
        using var scope = CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IIdentityAdministrationService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var user = await SeedUserAsync(db, UserRole.Viewer);

        var action = () => service.DeleteUserAsync(
            user.Id,
            user.Id,
            CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Você não pode excluir seu próprio usuário.");
    }

    [Fact]
    public async Task DeleteUser_LastActiveAdmin_ShouldThrowInvalidOperationException()
    {
        using var scope = CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IIdentityAdministrationService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var admin = await SeedUserAsync(db, UserRole.Admin);

        var action = () => service.DeleteUserAsync(
            admin.Id,
            Guid.NewGuid(),
            CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("O último administrador ativo não pode ser excluído.");
    }

    [Fact]
    public async Task DeleteUser_UnknownUser_ShouldReturnFalse()
    {
        using var scope = CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IIdentityAdministrationService>();

        var deleted = await service.DeleteUserAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CancellationToken.None);

        deleted.Should().BeFalse();
    }
}
