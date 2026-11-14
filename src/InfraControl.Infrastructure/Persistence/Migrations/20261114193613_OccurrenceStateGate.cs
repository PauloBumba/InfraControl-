using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OccurrenceStateGate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "occurrence_transitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurrenceKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    FromState = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ToState = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EventType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    SourceEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreateDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_occurrence_transitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "occurrences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurrenceKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreateDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_occurrences", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_occurrence_transitions_key_time",
                table: "occurrence_transitions",
                columns: new[] { "OccurrenceKey", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_occurrences_updated_at",
                table: "occurrences",
                column: "UpdatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "ux_occurrences_key",
                table: "occurrences",
                column: "OccurrenceKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "occurrence_transitions");

            migrationBuilder.DropTable(
                name: "occurrences");
        }
    }
}
