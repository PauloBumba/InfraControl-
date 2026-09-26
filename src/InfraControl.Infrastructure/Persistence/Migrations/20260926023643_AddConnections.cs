using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConnections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "connections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_interface_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_interface_id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreateDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_connections", x => x.id);
                    table.ForeignKey(
                        name: "FK_connections_network_interfaces_source_interface_id",
                        column: x => x.source_interface_id,
                        principalTable: "network_interfaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_connections_network_interfaces_target_interface_id",
                        column: x => x.target_interface_id,
                        principalTable: "network_interfaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_connections_source_interface_id_target_interface_id",
                table: "connections",
                columns: new[] { "source_interface_id", "target_interface_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_connections_target_interface_id",
                table: "connections",
                column: "target_interface_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "connections");
        }
    }
}
