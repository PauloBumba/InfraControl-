using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskSlasPauses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "task_sla_pauses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreateDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_sla_pauses", x => x.id);
                    table.ForeignKey(
                        name: "FK_task_sla_pauses_task_slas_task_id",
                        column: x => x.task_id,
                        principalTable: "task_slas",
                        principalColumn: "task_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_task_sla_pauses_task_id",
                table: "task_sla_pauses",
                column: "task_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "task_sla_pauses");
        }
    }
}
