using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRetentionPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "retention_policies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    days = table.Column<int>(type: "integer", nullable: false),
                    create_datetime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_datetime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_retention_policies", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_retention_policies_data_type",
                table: "retention_policies",
                column: "data_type",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "retention_policies");
        }
    }
}
