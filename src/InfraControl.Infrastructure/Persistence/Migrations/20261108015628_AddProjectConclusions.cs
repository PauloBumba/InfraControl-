using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectConclusions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "project_conclusions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    activities_performed = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    results_achieved = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    problems_encountered = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    solutions_implemented = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    evidence_summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    submitted_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decided_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decision_notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    create_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_conclusions", x => x.id);
                    table.ForeignKey(
                        name: "FK_project_conclusions_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_project_conclusions_project_id",
                table: "project_conclusions",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_project_conclusions_status",
                table: "project_conclusions",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_conclusions");
        }
    }
}
