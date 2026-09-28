using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskAssignees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CORRIGIDO (F7-T002): gerada contra assembly stale (--no-build) e
            // contentava erradamente o AddColumn "RootCause" (duplicando o
            // AddIncidentRootCause). As tabelas da Fase 7 são criadas em
            // AddTaskHistories (20260928001322) — esta migração é um no-op
            // proposital para bancos que ainda não a aplicaram.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op — ver Up.
        }
    }
}
