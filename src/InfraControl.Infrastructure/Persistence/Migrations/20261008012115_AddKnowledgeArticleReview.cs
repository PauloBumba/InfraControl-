using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeArticleReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "NextReviewAt",
                table: "knowledge_articles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewIntervalDays",
                table: "knowledge_articles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewResponsibleId",
                table: "knowledge_articles",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NextReviewAt",
                table: "knowledge_articles");

            migrationBuilder.DropColumn(
                name: "ReviewIntervalDays",
                table: "knowledge_articles");

            migrationBuilder.DropColumn(
                name: "ReviewResponsibleId",
                table: "knowledge_articles");
        }
    }
}
