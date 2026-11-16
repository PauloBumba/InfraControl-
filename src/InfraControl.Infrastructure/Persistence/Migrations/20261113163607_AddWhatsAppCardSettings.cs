using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppCardSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "card_incident_color",
                table: "whatsapp_integration_settings",
                type: "character varying(9)",
                maxLength: 9,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "card_incident_enabled",
                table: "whatsapp_integration_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "card_resolution_color",
                table: "whatsapp_integration_settings",
                type: "character varying(9)",
                maxLength: 9,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "card_resolution_enabled",
                table: "whatsapp_integration_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "card_warning_color",
                table: "whatsapp_integration_settings",
                type: "character varying(9)",
                maxLength: 9,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "card_warning_enabled",
                table: "whatsapp_integration_settings",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "card_window_hours",
                table: "whatsapp_integration_settings",
                type: "integer",
                nullable: false,
                defaultValue: 2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "card_incident_color",
                table: "whatsapp_integration_settings");

            migrationBuilder.DropColumn(
                name: "card_incident_enabled",
                table: "whatsapp_integration_settings");

            migrationBuilder.DropColumn(
                name: "card_resolution_color",
                table: "whatsapp_integration_settings");

            migrationBuilder.DropColumn(
                name: "card_resolution_enabled",
                table: "whatsapp_integration_settings");

            migrationBuilder.DropColumn(
                name: "card_warning_color",
                table: "whatsapp_integration_settings");

            migrationBuilder.DropColumn(
                name: "card_warning_enabled",
                table: "whatsapp_integration_settings");

            migrationBuilder.DropColumn(
                name: "card_window_hours",
                table: "whatsapp_integration_settings");
        }
    }
}
