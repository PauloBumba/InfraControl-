using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PdiDevelopmentStructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_individual_development_plans_user_id",
                table: "individual_development_plans",
                column: "user_id");

            migrationBuilder.CreateTable(
                name: "pdi_competencies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    create_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pdi_competencies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pdi_objectives",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    competency_id = table.Column<Guid>(type: "uuid", nullable: true),
                    current_situation = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    desired_situation = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    expected_result = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    indicator = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    target_date = table.Column<DateOnly>(type: "date", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    create_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pdi_objectives", x => x.id);
                    table.ForeignKey(
                        name: "FK_pdi_objectives_individual_development_plans_user_id",
                        column: x => x.user_id,
                        principalTable: "individual_development_plans",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_pdi_objectives_pdi_competencies_competency_id",
                        column: x => x.competency_id,
                        principalTable: "pdi_competencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pdi_strategies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    objective_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    create_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pdi_strategies", x => x.id);
                    table.ForeignKey(
                        name: "FK_pdi_strategies_pdi_objectives_objective_id",
                        column: x => x.objective_id,
                        principalTable: "pdi_objectives",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pdi_phases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    strategy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    responsible_id = table.Column<Guid>(type: "uuid", nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    completion_criteria = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    percent = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    create_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pdi_phases", x => x.id);
                    table.ForeignKey(
                        name: "FK_pdi_phases_pdi_strategies_strategy_id",
                        column: x => x.strategy_id,
                        principalTable: "pdi_strategies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pdi_activities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    phase_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    completed = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    create_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pdi_activities", x => x.id);
                    table.ForeignKey(
                        name: "FK_pdi_activities_pdi_phases_phase_id",
                        column: x => x.phase_id,
                        principalTable: "pdi_phases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pdi_activities_phase_id_sort_order",
                table: "pdi_activities",
                columns: new[] { "phase_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_pdi_competencies_name",
                table: "pdi_competencies",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "IX_pdi_objectives_competency_id",
                table: "pdi_objectives",
                column: "competency_id");

            migrationBuilder.CreateIndex(
                name: "IX_pdi_objectives_user_id_sort_order",
                table: "pdi_objectives",
                columns: new[] { "user_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_pdi_phases_strategy_id_sort_order",
                table: "pdi_phases",
                columns: new[] { "strategy_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_pdi_strategies_objective_id_sort_order",
                table: "pdi_strategies",
                columns: new[] { "objective_id", "sort_order" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pdi_activities");

            migrationBuilder.DropTable(
                name: "pdi_phases");

            migrationBuilder.DropTable(
                name: "pdi_strategies");

            migrationBuilder.DropTable(
                name: "pdi_objectives");

            migrationBuilder.DropTable(
                name: "pdi_competencies");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_individual_development_plans_user_id",
                table: "individual_development_plans");
        }
    }
}
