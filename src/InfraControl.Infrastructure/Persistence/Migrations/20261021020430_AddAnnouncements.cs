using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAnnouncements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "announcements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    fingerprint = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "info"),
                    priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    repeat_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    max_repeats = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    scheduled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    acknowledged_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_announcements", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_announcements_fingerprint",
                table: "announcements",
                column: "fingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_announcements_priority",
                table: "announcements",
                column: "priority");

            migrationBuilder.CreateIndex(
                name: "IX_announcements_scheduled_acknowledged",
                table: "announcements",
                columns: new[] { "scheduled_at", "acknowledged_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "announcements");
        }
    }
}
