using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSwitchPortSpeedDuplex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "duplex",
                table: "switch_ports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "speed",
                table: "switch_ports",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "duplex",
                table: "switch_ports");

            migrationBuilder.DropColumn(
                name: "speed",
                table: "switch_ports");
        }
    }
}
