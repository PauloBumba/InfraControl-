using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMacAddresses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mac_addresses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    interface_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mac = table.Column<string>(type: "character varying(17)", maxLength: 17, nullable: false),
                    CreateDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mac_addresses", x => x.id);
                    table.ForeignKey(
                        name: "FK_mac_addresses_network_interfaces_interface_id",
                        column: x => x.interface_id,
                        principalTable: "network_interfaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mac_addresses_interface_id",
                table: "mac_addresses",
                column: "interface_id");
            migrationBuilder.Sql("CREATE UNIQUE INDEX ux_mac_addresses_mac ON mac_addresses (LOWER(mac));");
    }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mac_addresses");
        }
    }
}
