using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AiVectorEmbeddingAnyDimension : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "embedding",
                table: "ai_vector_documents",
                type: "vector",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "vector(768)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "embedding",
                table: "ai_vector_documents",
                type: "vector(768)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "vector");
        }
    }
}
