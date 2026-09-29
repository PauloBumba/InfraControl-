using FluentAssertions;
using InfraControl.IntegrationTests.Common;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using InfraControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
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
    private const string MigrationSql = """
        UPDATE "DomainUserRoles" SET "Role" = 'Operator' WHERE "Role" = 'User';
        UPDATE "AspNetRoles" SET "Name" = 'Operator' WHERE "Name" = 'User';
        UPDATE "AspNetUsers"
           SET "SecurityStamp" = replace(gen_random_uuid()::text, '-', '')
         WHERE "SecurityStamp" IS NOT NULL;
        DELETE FROM "UserSessions";
        """;

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
                "LastSeenAt", "ExpiresAt", "CreateDateTime", "UpdateDateTime")
            VALUES ({0}, {1}, 'hash-refresh', now(), now(), now() + interval '7 days', now(), now());
            """, Guid.NewGuid(), domainUser.Id);

        // ── a migration ───────────────────────────────────────────────
        await dbContext.Database.ExecuteSqlRawAsync(MigrationSql);

        var domainRoles = await dbContext.Database
            .SqlQuery<string>(
                $"""SELECT "Role" AS "Value" FROM "DomainUserRoles" WHERE "UserId" = {domainUser.Id}""")
            .ToListAsync();
        domainRoles.Should().BeEquivalentTo(["Viewer", "Operator"]);

        var identityRoleName = await dbContext.Database
            .SqlQuery<string>($"""SELECT "Name" AS "Value" FROM "AspNetRoles" WHERE "Id" = {legacyRoleId}""")
            .FirstAsync();
        identityRoleName.Should().Be("Operator");

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
        await dbContext.Database.ExecuteSqlRawAsync(MigrationSql);
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

        await dbContext.Database.ExecuteSqlRawAsync(MigrationSql);

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
}
