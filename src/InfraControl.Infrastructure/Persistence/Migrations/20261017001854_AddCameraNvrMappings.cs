using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCameraNvrMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "camera_nvr_mappings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    camera_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nvr_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel_number = table.Column<int>(type: "integer", nullable: false),
                    CreateDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_camera_nvr_mappings", x => x.id);
                    table.ForeignKey(
                        name: "FK_camera_nvr_mappings_devices_camera_id",
                        column: x => x.camera_id,
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_camera_nvr_mappings_devices_nvr_id",
                        column: x => x.nvr_id,
                        principalTable: "devices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_camera_nvr_mappings_camera_id",
                table: "camera_nvr_mappings",
                column: "camera_id");

            migrationBuilder.CreateIndex(
                name: "ux_camera_nvr_mappings_nvr_channel",
                table: "camera_nvr_mappings",
                columns: new[] { "nvr_id", "channel_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "camera_nvr_mappings");
        }
    }
}
