using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSlos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "slos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    target_percent = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: false),
                    window_days = table.Column<int>(type: "integer", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreateDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_slos", x => x.id);
                    table.ForeignKey(
                        name: "FK_slos_services_service_id",
                        column: x => x.service_id,
                        principalTable: "services",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_slos_service_id",
                table: "slos",
                column: "service_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "slos");
        }
    }
}
