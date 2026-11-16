using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMonitoringProblemStates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "monitoring_problem_states",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    correlation_key = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: true),
                    severity = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    alert_id = table.Column<Guid>(type: "uuid", nullable: true),
                    incident_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_problem_event_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    open_count = table.Column<int>(type: "integer", nullable: false),
                    repeated_count = table.Column<int>(type: "integer", nullable: false),
                    opened_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreateDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_monitoring_problem_states", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_monitoring_problem_states_alert_id",
                table: "monitoring_problem_states",
                column: "alert_id");

            migrationBuilder.CreateIndex(
                name: "IX_monitoring_problem_states_source_correlation_key",
                table: "monitoring_problem_states",
                columns: new[] { "source", "correlation_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "monitoring_problem_states");
        }
    }
}
