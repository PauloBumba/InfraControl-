using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskPlanningAndCriteria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "PlannedEnd",
                table: "tasks",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PlannedStart",
                table: "tasks",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Result",
                table: "tasks",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "task_criteria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    IsDone = table.Column<bool>(type: "boolean", nullable: false),
                    done_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreateDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_criteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_task_criteria_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_task_criteria_task_id",
                table: "task_criteria",
                column: "task_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "task_criteria");

            migrationBuilder.DropColumn(
                name: "PlannedEnd",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "PlannedStart",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "Result",
                table: "tasks");
        }
    }
}
