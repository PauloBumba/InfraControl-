using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NotificationProtectionPhase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_notification_deliveries_notification_id",
                table: "notification_deliveries");

            migrationBuilder.AddColumn<Guid>(
                name: "aggregate_id",
                table: "notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "suppression_reason",
                table: "notification_deliveries",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_aggregate_id",
                table: "notifications",
                column: "aggregate_id");

            migrationBuilder.CreateIndex(
                name: "ux_notification_deliveries_dedup",
                table: "notification_deliveries",
                columns: new[] { "notification_id", "user_id", "channel" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_notifications_aggregate_id",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "ux_notification_deliveries_dedup",
                table: "notification_deliveries");

            migrationBuilder.DropColumn(
                name: "aggregate_id",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "suppression_reason",
                table: "notification_deliveries");

            migrationBuilder.CreateIndex(
                name: "IX_notification_deliveries_notification_id",
                table: "notification_deliveries",
                column: "notification_id");
        }
    }
}
