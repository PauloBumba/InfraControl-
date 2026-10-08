using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeArticleVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "version",
                table: "knowledge_articles",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "1.0");

            migrationBuilder.AddColumn<int>(
                name: "version_major_floor",
                table: "knowledge_articles",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "knowledge_article_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    article_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    content = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tags = table.Column<string[]>(type: "text[]", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreateDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_article_versions", x => x.id);
                    table.ForeignKey(
                        name: "FK_knowledge_article_versions_DomainUsers_approved_by_id",
                        column: x => x.approved_by_id,
                        principalTable: "DomainUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_knowledge_article_versions_DomainUsers_author_id",
                        column: x => x.author_id,
                        principalTable: "DomainUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_knowledge_article_versions_knowledge_articles_article_id",
                        column: x => x.article_id,
                        principalTable: "knowledge_articles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_article_versions_approved_by_id",
                table: "knowledge_article_versions",
                column: "approved_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_article_versions_author_id",
                table: "knowledge_article_versions",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ux_knowledge_article_versions_article_version",
                table: "knowledge_article_versions",
                columns: new[] { "article_id", "version" },
                unique: true);

            // F24-T003/K3 — backfill: os artigos que já existem viram a
            // v1.0 da própria linha de histórico (docs/32 §8: versão
            // alguma é perdida; o motivo fixo marca a captura inicial).
            // Idempotente (NOT EXISTS) e só roda nesta migração.
            migrationBuilder.Sql(
                """
                INSERT INTO knowledge_article_versions
                    (id, article_id, version, title, content, description, type, tags,
                     category_id, reason, author_id, approved_by_id, approved_at,
                     "CreateDateTime", "UpdateDateTime")
                SELECT gen_random_uuid(), a.id, '1.0', a.title, a.content, a.description,
                       a.type, COALESCE(a.tags, ARRAY[]::text[]), a.category_id,
                       'Criação do artigo', a.author_id, NULL, NULL,
                       a."CreateDateTime", a."CreateDateTime"
                FROM knowledge_articles a
                WHERE NOT EXISTS (
                    SELECT 1 FROM knowledge_article_versions v
                    WHERE v.article_id = a.id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "knowledge_article_versions");

            migrationBuilder.DropColumn(
                name: "version",
                table: "knowledge_articles");

            migrationBuilder.DropColumn(
                name: "version_major_floor",
                table: "knowledge_articles");
        }
    }
}
