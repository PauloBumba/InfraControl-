using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMessagingQueuePauses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "messaging_queue_pauses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    queue_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    vhost = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    connection_names = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    paused_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    paused_by_user_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    create_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_messaging_queue_pauses", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_messaging_queue_pauses_queue_name",
                table: "messaging_queue_pauses",
                column: "queue_name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "messaging_queue_pauses");
        }
    }
}
