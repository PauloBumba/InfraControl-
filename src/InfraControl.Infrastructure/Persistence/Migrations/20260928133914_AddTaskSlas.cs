using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskSlas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "task_slas",
                columns: table => new
                {
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sla_policy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    response_minutes = table.Column<int>(type: "integer", nullable: false),
                    resolution_minutes = table.Column<int>(type: "integer", nullable: false),
                    response_due_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    resolution_due_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreateDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_slas", x => x.task_id);
                    table.ForeignKey(
                        name: "FK_task_slas_sla_policies_sla_policy_id",
                        column: x => x.sla_policy_id,
                        principalTable: "sla_policies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_slas_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_task_slas_sla_policy_id",
                table: "task_slas",
                column: "sla_policy_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "task_slas");
        }
    }
}
