using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationCorrelationAndPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "source_alert_id",
                table: "notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_incident_id",
                table: "notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "previous_alert_id",
                table: "monitoring_problem_states",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "notification_policy_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    stabilization_warning_minutes = table.Column<int>(type: "integer", nullable: false),
                    stabilization_critical_minutes = table.Column<int>(type: "integer", nullable: false),
                    create_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_policy_settings", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_policy_settings");

            migrationBuilder.DropColumn(
                name: "source_alert_id",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "source_incident_id",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "previous_alert_id",
                table: "monitoring_problem_states");
        }
    }
}
