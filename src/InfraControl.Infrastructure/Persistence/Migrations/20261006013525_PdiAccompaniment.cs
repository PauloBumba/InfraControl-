using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PdiAccompaniment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pdi_check_ins",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    phase_id = table.Column<Guid>(type: "uuid", nullable: false),
                    check_in_date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    what_done = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    what_learned = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    difficulty = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    need_support = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    manager_feedback = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    next_action = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    responded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    responded_by = table.Column<Guid>(type: "uuid", nullable: true),
                    create_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pdi_check_ins", x => x.id);
                    table.ForeignKey(
                        name: "FK_pdi_check_ins_pdi_phases_phase_id",
                        column: x => x.phase_id,
                        principalTable: "pdi_phases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pdi_feedbacks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    phase_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    feedback_date = table.Column<DateOnly>(type: "date", nullable: false),
                    create_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pdi_feedbacks", x => x.id);
                    table.ForeignKey(
                        name: "FK_pdi_feedbacks_pdi_phases_phase_id",
                        column: x => x.phase_id,
                        principalTable: "pdi_phases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pdi_impediments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    phase_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    impact = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    occurred_at = table.Column<DateOnly>(type: "date", nullable: false),
                    responsible_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action_taken = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    resolved_at = table.Column<DateOnly>(type: "date", nullable: true),
                    create_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pdi_impediments", x => x.id);
                    table.ForeignKey(
                        name: "FK_pdi_impediments_pdi_phases_phase_id",
                        column: x => x.phase_id,
                        principalTable: "pdi_phases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pdi_progresses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    phase_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    percent = table.Column<int>(type: "integer", nullable: true),
                    comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    create_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_date_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pdi_progresses", x => x.id);
                    table.ForeignKey(
                        name: "FK_pdi_progresses_pdi_phases_phase_id",
                        column: x => x.phase_id,
                        principalTable: "pdi_phases",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pdi_check_ins_phase_id",
                table: "pdi_check_ins",
                column: "phase_id");

            migrationBuilder.CreateIndex(
                name: "IX_pdi_feedbacks_phase_id",
                table: "pdi_feedbacks",
                column: "phase_id");

            migrationBuilder.CreateIndex(
                name: "IX_pdi_impediments_phase_id",
                table: "pdi_impediments",
                column: "phase_id");

            migrationBuilder.CreateIndex(
                name: "IX_pdi_progresses_phase_id",
                table: "pdi_progresses",
                column: "phase_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pdi_check_ins");

            migrationBuilder.DropTable(
                name: "pdi_feedbacks");

            migrationBuilder.DropTable(
                name: "pdi_impediments");

            migrationBuilder.DropTable(
                name: "pdi_progresses");
        }
    }
}
