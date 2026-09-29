using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// ADR-016: <c>UserRole.User</c> → <c>UserRole.Operator</c>. O enum é
    /// persistido como string, então o rename da role é DADO, não esquema —
    /// por isso esta migration não altera o modelo e não tem Designer.
    ///
    /// O que muda:
    /// - "DomainUserRoles"."Role": 'User' → 'Operator' (papel do domínio);
    /// - "AspNetRoles"."Name": 'User' → 'Operator' (papel do Identity). O
    ///   "AspNetUserRoles"."RoleId" continua apontando para o mesmo Id, então
    ///   as atribuições não precisam ser tocadas;
    /// - "UserSessions" é esvaziada e o "SecurityStamp" é rotacionado, porque
    ///   o cookie de autenticação é auto-contido (AddCookie com
    ///   ClaimsPrincipal próprio) e carrega a claim de role antiga.
    ///
    /// Efeito de um cookie emitido antes da migration: ele só tem "User",
    /// que deixou de ser uma role válida — as policies Operate/Administer
    /// passam a NEGAR (fail-closed) e o usuário precisa entrar de novo. Não
    /// há caminho de escalada de privilégio. A leitura ([Authorize]) continua
    /// liberada, como antes.
    ///
    /// Idempotente: rodar de novo não encontra 'User' e não faz nada.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20260929010000_RenameUserRoleToOperator")]
    public partial class RenameUserRoleToOperator : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "DomainUserRoles" SET "Role" = 'Operator' WHERE "Role" = 'User';
                """);

            migrationBuilder.Sql(
                """
                UPDATE "AspNetRoles" SET "Name" = 'Operator' WHERE "Name" = 'User';
                """);

            // o cookie carrega a claim de role: rotaciona o stamp e derruba as
            // sessões de refresh para forçar reautenticação
            migrationBuilder.Sql(
                """
                UPDATE "AspNetUsers"
                   SET "SecurityStamp" = replace(gen_random_uuid()::text, '-', '')
                 WHERE "SecurityStamp" IS NOT NULL;
                """);

            migrationBuilder.Sql("""DELETE FROM "UserSessions";""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE "DomainUserRoles" SET "Role" = 'User' WHERE "Role" = 'Operator';
                """);

            migrationBuilder.Sql(
                """
                UPDATE "AspNetRoles" SET "Name" = 'User' WHERE "Name" = 'Operator';
                """);

            // as sessões removidas e o stamp anterior não são recuperáveis:
            // o usuário precisa entrar de novo
        }
    }
}
