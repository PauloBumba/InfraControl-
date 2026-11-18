using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSlaDefaultPolicyAndBreach : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "resolution_breached_at",
                table: "task_slas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "response_breached_at",
                table: "task_slas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_default",
                table: "sla_policies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_sla_policies_is_default",
                table: "sla_policies",
                column: "is_default",
                unique: true,
                filter: "is_default AND service_id IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_sla_policies_is_default",
                table: "sla_policies");

            migrationBuilder.DropColumn(
                name: "resolution_breached_at",
                table: "task_slas");

            migrationBuilder.DropColumn(
                name: "response_breached_at",
                table: "task_slas");

            migrationBuilder.DropColumn(
                name: "is_default",
                table: "sla_policies");
        }
    }
}
