using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AiProviderRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_providers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    base_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    api_key_encrypted = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    models = table.Column<string[]>(type: "text[]", nullable: false),
                    create_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_providers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ai_provider_capabilities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    capability = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    default_model = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    fallback_provider_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fallback_model = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    create_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_provider_capabilities", x => x.id);
                    table.ForeignKey(
                        name: "FK_ai_provider_capabilities_ai_providers_fallback_provider_id",
                        column: x => x.fallback_provider_id,
                        principalTable: "ai_providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ai_provider_capabilities_ai_providers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "ai_providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_provider_capabilities_capability",
                table: "ai_provider_capabilities",
                column: "capability",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_provider_capabilities_fallback_provider_id",
                table: "ai_provider_capabilities",
                column: "fallback_provider_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_provider_capabilities_provider_id",
                table: "ai_provider_capabilities",
                column: "provider_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_providers_name",
                table: "ai_providers",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_provider_capabilities");

            migrationBuilder.DropTable(
                name: "ai_providers");
        }
    }
}
