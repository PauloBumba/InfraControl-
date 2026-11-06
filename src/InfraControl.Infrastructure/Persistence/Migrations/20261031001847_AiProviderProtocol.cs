using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AiProviderProtocol : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "protocol",
                table: "ai_providers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "OpenAiCompatible");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "protocol",
                table: "ai_providers");
        }
    }
}
