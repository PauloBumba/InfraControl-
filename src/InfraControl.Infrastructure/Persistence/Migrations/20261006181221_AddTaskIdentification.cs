using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskIdentification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "DueDate",
                table: "tasks",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "tasks",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                // F23-T001: backfill das tasks existentes com o padrão
                // Medium (hand-edit — o EF gerava ""; o ValueConverter
                // falharia ao ler ""). Mesmo precedente de alerts.source.
                defaultValue: "Medium");

            migrationBuilder.AddColumn<Guid>(
                name: "ResponsibleId",
                table: "tasks",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tasks_Priority",
                table: "tasks",
                column: "Priority");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tasks_Priority",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "tasks");

            migrationBuilder.DropColumn(
                name: "ResponsibleId",
                table: "tasks");
        }
    }
}
