using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSloCheckBindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "slo_check_bindings",
                columns: table => new
                {
                    slo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    monitoring_check_id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreateDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_slo_check_bindings", x => new { x.slo_id, x.monitoring_check_id });
                    table.ForeignKey(
                        name: "FK_slo_check_bindings_monitoring_checks_monitoring_check_id",
                        column: x => x.monitoring_check_id,
                        principalTable: "monitoring_checks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_slo_check_bindings_slos_slo_id",
                        column: x => x.slo_id,
                        principalTable: "slos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_slo_check_bindings_monitoring_check_id",
                table: "slo_check_bindings",
                column: "monitoring_check_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "slo_check_bindings");
        }
    }
}
