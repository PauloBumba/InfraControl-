using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationRouting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_routing_policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    defaults = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    create_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_routing_policies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification_routing_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    min_severity = table.Column<int>(type: "integer", nullable: true),
                    max_severity = table.Column<int>(type: "integer", nullable: true),
                    host_ids = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    host_group_ids = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    trigger_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    tags = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    channels = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    create_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_routing_rules", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notification_routing_rules_event_type",
                table: "notification_routing_rules",
                column: "event_type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_routing_policies");

            migrationBuilder.DropTable(
                name: "notification_routing_rules");
        }
    }
}
