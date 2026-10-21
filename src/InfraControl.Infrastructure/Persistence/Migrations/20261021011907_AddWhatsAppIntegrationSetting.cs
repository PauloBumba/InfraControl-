using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppIntegrationSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "DomainUsers",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "whatsapp_integration_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    base_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    api_key_encrypted = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    instance_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    group_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    phone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    connection_state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    last_tested_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_test_ok = table.Column<bool>(type: "boolean", nullable: false),
                    last_test_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    create_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_integration_settings", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "whatsapp_integration_settings");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "DomainUsers");
        }
    }
}
