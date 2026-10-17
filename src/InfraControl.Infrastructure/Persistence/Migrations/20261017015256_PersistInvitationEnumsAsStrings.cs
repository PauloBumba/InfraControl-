using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersistInvitationEnumsAsStrings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convite/status e papel do convite eram os últimos enums ainda
            // persistidos como integer (todo o resto do modelo já é string —
            // ver HasConversion<string> nas configurations). O AlterColumn
            // padrão do scaffold não converte dados no Postgres; o USING
            // mapeia cada valor antigo para o nome do enum (fonte: InvitationStatus
            // = 1..6, UserRole = 1..3).
            migrationBuilder.Sql("""
                ALTER TABLE invitations
                    ALTER COLUMN status TYPE character varying(32)
                    USING (CASE status
                        WHEN 1 THEN 'Pending'
                        WHEN 2 THEN 'Accepted'
                        WHEN 3 THEN 'Rejected'
                        WHEN 4 THEN 'Expired'
                        WHEN 5 THEN 'Cancelled'
                        WHEN 6 THEN 'Activated' END);
                """);

            migrationBuilder.Sql("""
                ALTER TABLE invitation_roles
                    ALTER COLUMN role TYPE character varying(32)
                    USING (CASE role
                        WHEN 1 THEN 'Viewer'
                        WHEN 2 THEN 'Operator'
                        WHEN 3 THEN 'Admin' END);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE invitations
                    ALTER COLUMN status TYPE integer
                    USING (CASE status
                        WHEN 'Pending' THEN 1
                        WHEN 'Accepted' THEN 2
                        WHEN 'Rejected' THEN 3
                        WHEN 'Expired' THEN 4
                        WHEN 'Cancelled' THEN 5
                        WHEN 'Activated' THEN 6 END);
                """);

            migrationBuilder.Sql("""
                ALTER TABLE invitation_roles
                    ALTER COLUMN role TYPE integer
                    USING (CASE role
                        WHEN 'Viewer' THEN 1
                        WHEN 'Operator' THEN 2
                        WHEN 'Admin' THEN 3 END);
                """);
        }
    }
}
