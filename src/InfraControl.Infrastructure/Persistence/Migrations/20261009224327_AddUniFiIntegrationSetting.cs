using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUniFiIntegrationSetting : Migration
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
                name: "pdi_evidences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    phase_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    extension = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    content_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    create_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pdi_evidences", x => x.id);
                    table.ForeignKey(
                        name: "FK_pdi_evidences_pdi_phases_phase_id",
                        column: x => x.phase_id,
                        principalTable: "pdi_phases",
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

            migrationBuilder.CreateTable(
                name: "unifi_integration_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    username = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    password_encrypted = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    site = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    allow_invalid_tls_certificate = table.Column<bool>(type: "boolean", nullable: false),
                    create_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unifi_integration_settings", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pdi_assessments_plan_id_side",
                table: "pdi_assessments",
                columns: new[] { "plan_id", "side" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pdi_evidences_phase_id",
                table: "pdi_evidences",
                column: "phase_id");

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
                name: "pdi_evidences");

            migrationBuilder.DropTable(
                name: "pdi_results");

            migrationBuilder.DropTable(
                name: "unifi_integration_settings");
        }
    }
}
