using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeArticleIdentification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "author_id",
                table: "knowledge_articles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "knowledge_articles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "responsible_id",
                table: "knowledge_articles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "knowledge_articles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "knowledge_article_tags",
                columns: table => new
                {
                    article_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CreateDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_article_tags", x => new { x.article_id, x.tag });
                    table.ForeignKey(
                        name: "FK_knowledge_article_tags_knowledge_articles_article_id",
                        column: x => x.article_id,
                        principalTable: "knowledge_articles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "knowledge_article_tags");

            migrationBuilder.DropColumn(
                name: "author_id",
                table: "knowledge_articles");

            migrationBuilder.DropColumn(
                name: "description",
                table: "knowledge_articles");

            migrationBuilder.DropColumn(
                name: "responsible_id",
                table: "knowledge_articles");

            migrationBuilder.DropColumn(
                name: "type",
                table: "knowledge_articles");
        }
    }
}
