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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "unifi_integration_settings");
        }
    }
}
