using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMonitoringEventsAndAlertExternalSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "kind",
                table: "alerts",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<Guid>(
                name: "device_id",
                table: "alerts",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "external_problem_id",
                table: "alerts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "monitoring_event_id",
                table: "alerts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source",
                table: "alerts",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                // Linhas pré-ADR-027 são do pipeline nativo — sem este default
                // o backfill gravaria '' e quebraria o filtro Source='Native'.
                defaultValue: "Native");

            migrationBuilder.CreateTable(
                name: "monitoring_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    external_event_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    external_problem_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    external_recovery_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    device_id = table.Column<Guid>(type: "uuid", nullable: true),
                    check_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    severity = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    recovered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    raw_payload = table.Column<string>(type: "text", nullable: true),
                    CreateDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_monitoring_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_monitoring_events_devices_device_id",
                        column: x => x.device_id,
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_alerts_monitoring_event_id",
                table: "alerts",
                column: "monitoring_event_id");

            migrationBuilder.CreateIndex(
                name: "IX_alerts_source_external_problem_id",
                table: "alerts",
                columns: new[] { "source", "external_problem_id" });

            migrationBuilder.CreateIndex(
                name: "IX_monitoring_events_device_id",
                table: "monitoring_events",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "IX_monitoring_events_source_external_event_id",
                table: "monitoring_events",
                columns: new[] { "source", "external_event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_monitoring_events_source_external_problem_id",
                table: "monitoring_events",
                columns: new[] { "source", "external_problem_id" });

            migrationBuilder.AddForeignKey(
                name: "FK_alerts_monitoring_events_monitoring_event_id",
                table: "alerts",
                column: "monitoring_event_id",
                principalTable: "monitoring_events",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_alerts_monitoring_events_monitoring_event_id",
                table: "alerts");

            migrationBuilder.DropTable(
                name: "monitoring_events");

            migrationBuilder.DropIndex(
                name: "IX_alerts_monitoring_event_id",
                table: "alerts");

            migrationBuilder.DropIndex(
                name: "IX_alerts_source_external_problem_id",
                table: "alerts");

            migrationBuilder.DropColumn(
                name: "external_problem_id",
                table: "alerts");

            migrationBuilder.DropColumn(
                name: "monitoring_event_id",
                table: "alerts");

            migrationBuilder.DropColumn(
                name: "source",
                table: "alerts");

            migrationBuilder.AlterColumn<string>(
                name: "kind",
                table: "alerts",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "device_id",
                table: "alerts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
