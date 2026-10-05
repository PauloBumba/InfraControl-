using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PdiLifecycleManagerPeriodHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "manager_id",
                table: "individual_development_plans",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "period_end",
                table: "individual_development_plans",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "period_start",
                table: "individual_development_plans",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "individual_development_plan_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    to_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    create_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_individual_development_plan_history", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_individual_development_plan_history_user_id_create_date_time",
                table: "individual_development_plan_history",
                columns: new[] { "user_id", "create_date_time" });

            // Remapeamento de dados (ADR-029 §2): "InProgress" passou a
            // significar "Em acompanhamento"; o estado vigente antigo vira
            // "Active".
            migrationBuilder.Sql(
                "UPDATE individual_development_plans SET status = 'Active' WHERE status = 'InProgress';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "individual_development_plan_history");

            migrationBuilder.DropColumn(
                name: "manager_id",
                table: "individual_development_plans");

            migrationBuilder.DropColumn(
                name: "period_end",
                table: "individual_development_plans");

            migrationBuilder.DropColumn(
                name: "period_start",
                table: "individual_development_plans");
        }
    }
}
