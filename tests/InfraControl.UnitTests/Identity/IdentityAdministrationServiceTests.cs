using FluentAssertions;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Auth;
using InfraControl.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using DomainIdentity = InfraControl.Domain.Entities;

namespace InfraControl.UnitTests.Identity;

/// <summary>
/// F18-T004 — migração dos testes que cobriam o slice removido junto com
/// o `RolesController` (`api/identity/users`, fora do v1, DELETE
/// duplicado): o caminho vivo da gestão de papéis é o
/// `IIdentityAdministrationService` do `api/v1/admin/users`. Estes facts
/// reproduzem os fatos que `RoleCommandTests` cobria nos comandos órfãos
/// — atribuição persiste, usuário desconhecido responde false (o
/// controller traduz em 404) e o último admin ativo não cai (409).
///
/// A migração expôs um bug real de produção: `UserRoleAssignment.Id` era
/// `ValueGeneratedOnAdd` (convenção Guid) mas nasce preenchido no
/// `Entity()`, então o DetectChanges anexava a filha NOVA como Modified
/// e o save estourava `DbUpdateConcurrencyException` (500 em
/// `POST/DELETE /admin/users/{id}/roles/{role}` — nunca houve teste de
/// rota; os comandos usavam repositório fake). Corrigido em
/// `UserRoleAssignmentConfiguration` com `ValueGeneratedNever` — os
/// facts de persistência são o teste de regressão.
/// </summary>
public sealed class IdentityAdministrationServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDbContext> _options;

    public IdentityAdministrationServiceTests()
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

    private static async Task<DomainIdentity.User> SeedUserAsync(AppDbContext context, UserRole role)
    {
        var user = DomainIdentity.User.Create(Guid.NewGuid(), $"{role.ToString().ToLowerInvariant()}@example.com", "Operador", role);
        context.DomainUsers.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task AssignRole_ShouldPersistOnDomainUser()
    {
        await using var context = await CreateContextAsync();
        var user = await SeedUserAsync(context, UserRole.Viewer);

        var service = new IdentityAdministrationService(context);
        var found = await service.AssignRoleAsync(user.Id, UserRole.Operator, CancellationToken.None);

        found.Should().BeTrue();
        user.Roles.Should().Contain([UserRole.Viewer, UserRole.Operator]);
    }

    [Fact]
    public async Task AssignRole_UnknownUser_ShouldReturnFalseFor404()
    {
        await using var context = await CreateContextAsync();
        var service = new IdentityAdministrationService(context);

        var found = await service.AssignRoleAsync(Guid.NewGuid(), UserRole.Operator, CancellationToken.None);

        found.Should().BeFalse("o controller traduz false em 404 no envelope");
    }

    [Fact]
    public async Task RemoveRole_LastActiveAdmin_ShouldThrowInvalidOperationExceptionPtBr()
    {
        await using var context = await CreateContextAsync();
        var admin = await SeedUserAsync(context, UserRole.Admin);

        var service = new IdentityAdministrationService(context);
        var action = () => service.RemoveRoleAsync(admin.Id, UserRole.Admin, CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("O último administrador ativo não pode perder a função Admin.");
        admin.Roles.Should().Contain(
            UserRole.Admin, "a invariante é verificada antes de qualquer remoção");
    }

    [Fact]
    public async Task RemoveRole_UnknownUser_ShouldReturnFalseFor404()
    {
        await using var context = await CreateContextAsync();
        var service = new IdentityAdministrationService(context);

        var removed = await service.RemoveRoleAsync(Guid.NewGuid(), UserRole.Admin, CancellationToken.None);

        removed.Should().BeFalse("o controller traduz false em 404 no envelope");
    }

    [Fact]
    public async Task RemoveRole_RegularRole_ShouldPersistRemoval()
    {
        await using var context = await CreateContextAsync();
        var user = await SeedUserAsync(context, UserRole.Admin);
        user.AssignRole(UserRole.Operator);
        await context.SaveChangesAsync();

        var service = new IdentityAdministrationService(context);
        var removed = await service.RemoveRoleAsync(user.Id, UserRole.Operator, CancellationToken.None);

        removed.Should().BeTrue();
        user.Roles.Should().BeEquivalentTo([UserRole.Admin]);
    }

    [Fact]
    public async Task UpdateUser_ShouldPersistChanges()
    {
        await using var context = await CreateContextAsync();
        var user = await SeedUserAsync(context, UserRole.Viewer);

        var service = new IdentityAdministrationService(context);
        var updated = await service.UpdateUserAsync(user.Id, "Novo Nome", "novo@example.com", CancellationToken.None);

        updated.Should().BeTrue();
        user.DisplayName.Should().Be("Novo Nome");
        user.Email.Should().Be("novo@example.com");
    }

    [Fact]
    public async Task UpdateUser_UnknownUser_ShouldReturnFalseFor404()
    {
        await using var context = await CreateContextAsync();
        var service = new IdentityAdministrationService(context);

        var updated = await service.UpdateUserAsync(Guid.NewGuid(), "Nome", "email@example.com", CancellationToken.None);

        updated.Should().BeFalse("o controller traduz false em 404 no envelope");
    }

    [Fact]
    public async Task UpdateUser_DuplicateEmail_ShouldThrowInvalidOperationExceptionPtBr()
    {
        await using var context = await CreateContextAsync();
        var user1 = await SeedUserAsync(context, UserRole.Viewer);
        var user2 = await SeedUserAsync(context, UserRole.Admin);

        var service = new IdentityAdministrationService(context);
        var action = () => service.UpdateUserAsync(user2.Id, "Admin", user1.Email, CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Já existe um usuário com este e-mail.");
    }

    [Fact]
    public async Task DeleteUser_ShouldRevokeSessionsAndRemoveUser()
    {
        await using var context = await CreateContextAsync();
        var user = await SeedUserAsync(context, UserRole.Viewer);
        var session = DomainIdentity.UserSession.Start(user.Id, "refresh-token", TimeSpan.FromHours(1), TimeSpan.FromMinutes(30));
        context.UserSessions.Add(session);
        await context.SaveChangesAsync();

        var service = new IdentityAdministrationService(context);
        var deleted = await service.DeleteUserAsync(user.Id, Guid.NewGuid(), CancellationToken.None);

        deleted.Should().BeTrue();
        session.RevokedAt.Should().NotBeNull();
        context.DomainUsers.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteUser_UnknownUser_ShouldReturnFalseFor404()
    {
        await using var context = await CreateContextAsync();
        var service = new IdentityAdministrationService(context);

        var deleted = await service.DeleteUserAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        deleted.Should().BeFalse("o controller traduz false em 404 no envelope");
    }

    [Fact]
    public async Task DeleteUser_SelfDelete_ShouldThrowInvalidOperationExceptionPtBr()
    {
        await using var context = await CreateContextAsync();
        var user = await SeedUserAsync(context, UserRole.Viewer);

        var service = new IdentityAdministrationService(context);
        var action = () => service.DeleteUserAsync(user.Id, user.Id, CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Você não pode excluir seu próprio usuário.");
    }

    [Fact]
    public async Task DeleteUser_LastActiveAdmin_ShouldThrowInvalidOperationExceptionPtBr()
    {
        await using var context = await CreateContextAsync();
        var admin = await SeedUserAsync(context, UserRole.Admin);

        var service = new IdentityAdministrationService(context);
        var action = () => service.DeleteUserAsync(admin.Id, Guid.NewGuid(), CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("O último administrador ativo não pode ser excluído.");
    }
}
