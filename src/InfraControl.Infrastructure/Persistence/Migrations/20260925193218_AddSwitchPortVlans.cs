using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSwitchPortVlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "switch_port_vlans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    switch_port_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vlan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreateDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_switch_port_vlans", x => x.id);
                    table.ForeignKey(
                        name: "FK_switch_port_vlans_switch_ports_switch_port_id",
                        column: x => x.switch_port_id,
                        principalTable: "switch_ports",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_switch_port_vlans_vlans_vlan_id",
                        column: x => x.vlan_id,
                        principalTable: "vlans",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_switch_port_vlans_switch_port_id_vlan_id",
                table: "switch_port_vlans",
                columns: new[] { "switch_port_id", "vlan_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_switch_port_vlans_vlan_id",
                table: "switch_port_vlans",
                column: "vlan_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "switch_port_vlans");
        }
    }
}
