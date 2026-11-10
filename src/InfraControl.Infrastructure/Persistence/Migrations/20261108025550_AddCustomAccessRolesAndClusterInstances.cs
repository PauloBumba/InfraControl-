using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomAccessRolesAndClusterInstances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_access_policy_rules_role_module_key",
                table: "access_policy_rules");

            migrationBuilder.DropColumn(
                name: "role",
                table: "access_policy_rules");

            migrationBuilder.AddColumn<string>(
                name: "role_key",
                table: "access_policy_rules",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "access_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    create_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_access_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "service_instances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    host = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    port = table.Column<int>(type: "integer", nullable: true),
                    health_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    weight = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    last_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    last_checked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    create_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_instances", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_access_roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    create_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_access_roles", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_access_policy_rules_role_key_module_key",
                table: "access_policy_rules",
                columns: new[] { "role_key", "module_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_access_roles_key",
                table: "access_roles",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_instances_kind",
                table: "service_instances",
                column: "kind");

            migrationBuilder.CreateIndex(
                name: "IX_user_access_roles_user_id_role_key",
                table: "user_access_roles",
                columns: new[] { "user_id", "role_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "access_roles");

            migrationBuilder.DropTable(
                name: "service_instances");

            migrationBuilder.DropTable(
                name: "user_access_roles");

            migrationBuilder.DropIndex(
                name: "IX_access_policy_rules_role_key_module_key",
                table: "access_policy_rules");

            migrationBuilder.DropColumn(
                name: "role_key",
                table: "access_policy_rules");

            migrationBuilder.AddColumn<string>(
                name: "role",
                table: "access_policy_rules",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_access_policy_rules_role_module_key",
                table: "access_policy_rules",
                columns: new[] { "role", "module_key" },
                unique: true);
        }
    }
}
