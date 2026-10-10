using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddZabbixIntegrationSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "zabbix_integration_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    api_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    username = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    password_encrypted = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    detected_version = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    last_communication_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_tested_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_test_ok = table.Column<bool>(type: "boolean", nullable: false),
                    last_test_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    create_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zabbix_integration_settings", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "zabbix_integration_settings");
        }
    }
}
