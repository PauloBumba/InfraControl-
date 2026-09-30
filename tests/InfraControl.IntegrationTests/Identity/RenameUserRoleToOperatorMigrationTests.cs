using FluentAssertions;
using InfraControl.IntegrationTests.Common;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using InfraControl.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using System.Reflection;
using InfraControl.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace InfraControl.IntegrationTests.Identity;

/// <summary>
/// ADR-016: <c>UserRole.User</c> → <c>Operator</c> é DADO persistido (o enum é
/// gravado como string), não esquema. A migration executa SQL cru, então é
/// testada contra o Postgres de verdade: erro de nome de tabela ou de aspas
/// só apareceria em produção, e aqui o efeito é falha de autorização, não de
/// start.
/// </summary>
public sealed class RenameUserRoleToOperatorMigrationTests : BaseIntegrationTest
{
    /// <summary>
    /// O SQL vem da CLASSE da migration, não de uma cópia no teste: uma cópia
    /// passa verde enquanto a migration real continua quebrada.
    /// </summary>
    private static IReadOnlyList<string> Statements(string direction = "Up")
    {
        var builder = new MigrationBuilder("Npgsql");
        var method = typeof(RenameUserRoleToOperator).GetMethod(
            direction, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Migration sem {direction}().");

        method.Invoke(new RenameUserRoleToOperator(), [builder]);

        return builder.Operations
            .OfType<SqlOperation>()
            .Select(operation => operation.Sql)
            .ToList();
    }

    private static async Task RunAsync(AppDbContext dbContext, string direction = "Up")
    {
        foreach (var sql in Statements(direction))
            await dbContext.Database.ExecuteSqlRawAsync(sql);
    }

    [Fact]
    public async Task Migration_Should_Rename_Legacy_Role_In_Domain_And_Identity()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // ── estado legado: papel 'User' nas duas tabelas ──────────────
        var domainUser = User.Create(Guid.NewGuid(), "operador@exemplo.test", "Operador");
        dbContext.DomainUsers.Add(domainUser);
        await dbContext.SaveChangesAsync();

        // o papel legado é inserido cru: o enum atual não tem mais 'User',
        // então o caminho normal do aggregate não conseguiria criá-lo
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "DomainUserRoles" ("Id", "UserId", "Role", "CreateDateTime", "UpdateDateTime")
            VALUES ({0}, {1}, 'User', now(), now());
            """, Guid.NewGuid(), domainUser.Id);

        var legacyRoleId = Guid.NewGuid();
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "AspNetUsers" ("Id", "UserName", "NormalizedUserName", "Email",
                "NormalizedEmail", "EmailConfirmed", "PasswordHash", "SecurityStamp",
                "ConcurrencyStamp", "LockoutEnd", "LockoutEnabled", "TwoFactorEnabled",
                "PhoneNumberConfirmed", "AccessFailedCount")
            VALUES ({0}, 'operador', 'OPERADOR', 'operador@exemplo.test', 'OPERADOR@EXEMPLO.TEST',
                true, 'hash', 'stamp-antigo', 'conc', NULL, false, false, false, 0);
            """, domainUser.Id);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
            VALUES ({0}, 'User', 'USER', 'conc');
            """, legacyRoleId);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "AspNetUserRoles" ("UserId", "RoleId") VALUES ({0}, {1});
            """, domainUser.Id, legacyRoleId);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "UserSessions" ("Id", "UserId", "RefreshTokenHash", "CreatedAt",
                "LastSeenAt", "ExpiresAt", "AbsoluteExpiresAt", "CreateDateTime", "UpdateDateTime")
            VALUES ({0}, {1}, 'hash-refresh', now(), now(), now() + interval '7 days', now() + interval '7 days', now(), now());
            """, Guid.NewGuid(), domainUser.Id);

        // ── a migration ───────────────────────────────────────────────
        await RunAsync(dbContext);

        var domainRoles = await dbContext.Database
            .SqlQuery<string>(
                $"""SELECT "Role" AS "Value" FROM "DomainUserRoles" WHERE "UserId" = {domainUser.Id}""")
            .ToListAsync();
        domainRoles.Should().BeEquivalentTo(["Viewer", "Operator"]);

        var identityRoleName = await dbContext.Database
            .SqlQuery<string>($"""SELECT "Name" AS "Value" FROM "AspNetRoles" WHERE "Id" = {legacyRoleId}""")
            .FirstAsync();
        identityRoleName.Should().Be("Operator");

        // a coluna que o Identity realmente usa para achar a role. Se ela
        // continuasse 'USER', FindByNameAsync("Operator") não acharia a linha
        // e o RoleManager criaria uma role 'Operator' duplicada e vazia.
        var identityRoleNormalized = await dbContext.Database
            .SqlQuery<string>(
                $"""SELECT "NormalizedName" AS "Value" FROM "AspNetRoles" WHERE "Id" = {legacyRoleId}""")
            .FirstAsync();
        identityRoleNormalized.Should().Be("OPERATOR");

        // e nenhuma linha pode ficar com a chave de lookup antiga, senão o
        // RoleManager não acha 'Operator' e cria uma segunda role órfã
        (await dbContext.Database
                .SqlQuery<int>(
                    $"""SELECT count(*) AS "Value" FROM "AspNetRoles" WHERE "NormalizedName" = 'USER'""")
                .FirstAsync())
            .Should().Be(0);

        // a atribuição continua válida: mesmo RoleId, agora chamado 'Operator'
        var stillAssigned = await dbContext.Database
            .SqlQuery<Guid>(
                $"""SELECT "RoleId" AS "Value" FROM "AspNetUserRoles" WHERE "UserId" = {domainUser.Id}""")
            .FirstAsync();
        stillAssigned.Should().Be(legacyRoleId);

        // sessão revogada e stamp rotacionado: o cookie antigo morre com ele
        (await dbContext.UserSessions.CountAsync()).Should().Be(0);
        var stamp = await dbContext.Database
            .SqlQuery<string>(
                $"""SELECT "SecurityStamp" AS "Value" FROM "AspNetUsers" WHERE "Id" = {domainUser.Id}""")
            .FirstAsync();
        stamp.Should().NotBe("stamp-antigo");

        // idempotente: rodar de novo não duplica nem quebra o índice único
        await RunAsync(dbContext);
        (await dbContext.Database
                .SqlQuery<string>(
                    $"""SELECT "Role" AS "Value" FROM "DomainUserRoles" WHERE "UserId" = {domainUser.Id}""")
                .ToListAsync())
            .Should().BeEquivalentTo(["Viewer", "Operator"]);
    }

    [Fact]
    public async Task Renamed_Role_Should_Belong_To_Operate_And_Not_Admit_A_Stale_User_Claim()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var domainUser = User.Create(Guid.NewGuid(), "viewer@exemplo.test", "Viewer");
        dbContext.DomainUsers.Add(domainUser);
        await dbContext.SaveChangesAsync();
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "DomainUserRoles" ("Id", "UserId", "Role", "CreateDateTime", "UpdateDateTime")
            VALUES ({0}, {1}, 'User', now(), now());
            """, Guid.NewGuid(), domainUser.Id);

        await RunAsync(dbContext);

        var granted = await dbContext.Database
            .SqlQuery<string>(
                $"""SELECT "Role" AS "Value" FROM "DomainUserRoles" WHERE "UserId" = {domainUser.Id}""")
            .ToListAsync();

        // 'Operator' opera; o claim antigo 'User' não pertence a nenhum nível
        granted.Should().Contain("Operator");
        granted.Should().NotContain("User");
        RoleAccess.Operate.Should().Contain(granted.Where(role => role == "Operator"));
        RoleAccess.Operate.Should().NotContain("User");
    }

    [Fact]
    public async Task Down_Should_Bring_Both_Role_Names_Back()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var domainUser = User.Create(Guid.NewGuid(), "rollback@exemplo.test", "Rollback");
        dbContext.DomainUsers.Add(domainUser);
        await dbContext.SaveChangesAsync();

        var roleId = Guid.NewGuid();
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "DomainUserRoles" ("Id", "UserId", "Role", "CreateDateTime", "UpdateDateTime")
            VALUES ({0}, {1}, 'Operator', now(), now());
            """, Guid.NewGuid(), domainUser.Id);
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
            VALUES ({0}, 'Operator', 'OPERATOR', 'conc');
            """, roleId);

        await RunAsync(dbContext, "Down");

        // o nome e a chave de lookup voltam juntos: metade disso deixaria o
        // RoleManager sem conseguir encontrar a role
        var restoredName = await dbContext.Database
            .SqlQuery<string>(
                $"""SELECT "Name" AS "Value" FROM "AspNetRoles" WHERE "Id" = {roleId}""")
            .FirstAsync();
        restoredName.Should().Be("User");

        var restoredNormalized = await dbContext.Database
            .SqlQuery<string>(
                $"""SELECT "NormalizedName" AS "Value" FROM "AspNetRoles" WHERE "Id" = {roleId}""")
            .FirstAsync();
        restoredNormalized.Should().Be("USER");

        (await dbContext.Database
                .SqlQuery<string>(
                    $"""SELECT "Role" AS "Value" FROM "DomainUserRoles" WHERE "UserId" = {domainUser.Id}""")
                .ToListAsync())
            .Should().Contain("User")   // o papel volta ao valor legado
            .And.NotContain("Operator");
    }
}
