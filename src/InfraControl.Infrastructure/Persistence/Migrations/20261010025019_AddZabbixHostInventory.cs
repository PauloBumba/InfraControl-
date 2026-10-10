using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddZabbixHostInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "last_inventory_sync_at_utc",
                table: "zabbix_integration_settings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "zabbix_hosts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    host_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    host = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    port = table.Column<int>(type: "integer", nullable: true),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    discovered = table.Column<bool>(type: "boolean", nullable: false),
                    groups_json = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    templates_json = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    last_synced_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    missing_since_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    create_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zabbix_hosts", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_zabbix_hosts_host_id",
                table: "zabbix_hosts",
                column: "host_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "zabbix_hosts");

            migrationBuilder.DropColumn(
                name: "last_inventory_sync_at_utc",
                table: "zabbix_integration_settings");
        }
    }
}
