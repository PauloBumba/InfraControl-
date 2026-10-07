using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PdiEvaluation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pdi_assessments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    side = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    create_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pdi_assessments", x => x.id);
                    table.ForeignKey(
                        name: "FK_pdi_assessments_individual_development_plans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "individual_development_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pdi_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    finalized_by = table.Column<Guid>(type: "uuid", nullable: false),
                    finalized_at = table.Column<DateOnly>(type: "date", nullable: false),
                    create_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pdi_results", x => x.id);
                    table.ForeignKey(
                        name: "FK_pdi_results_individual_development_plans_plan_id",
                        column: x => x.plan_id,
                        principalTable: "individual_development_plans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pdi_assessments_plan_id_side",
                table: "pdi_assessments",
                columns: new[] { "plan_id", "side" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pdi_results_plan_id",
                table: "pdi_results",
                column: "plan_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pdi_assessments");

            migrationBuilder.DropTable(
                name: "pdi_results");
        }
    }
}
