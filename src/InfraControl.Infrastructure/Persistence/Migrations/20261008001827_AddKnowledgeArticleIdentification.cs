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

            migrationBuilder.AddColumn<string[]>(
                name: "tags",
                table: "knowledge_articles",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string>(
                name: "type",
                table: "knowledge_articles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_articles_author_id",
                table: "knowledge_articles",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_articles_responsible_id",
                table: "knowledge_articles",
                column: "responsible_id");

            migrationBuilder.AddForeignKey(
                name: "FK_knowledge_articles_DomainUsers_author_id",
                table: "knowledge_articles",
                column: "author_id",
                principalTable: "DomainUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_knowledge_articles_DomainUsers_responsible_id",
                table: "knowledge_articles",
                column: "responsible_id",
                principalTable: "DomainUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_knowledge_articles_DomainUsers_author_id",
                table: "knowledge_articles");

            migrationBuilder.DropForeignKey(
                name: "FK_knowledge_articles_DomainUsers_responsible_id",
                table: "knowledge_articles");

            migrationBuilder.DropIndex(
                name: "IX_knowledge_articles_author_id",
                table: "knowledge_articles");

            migrationBuilder.DropIndex(
                name: "IX_knowledge_articles_responsible_id",
                table: "knowledge_articles");

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
                name: "tags",
                table: "knowledge_articles");

            migrationBuilder.DropColumn(
                name: "type",
                table: "knowledge_articles");
        }
    }
}
