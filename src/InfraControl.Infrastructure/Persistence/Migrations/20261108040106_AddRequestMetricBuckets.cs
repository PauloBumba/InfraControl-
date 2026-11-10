using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestMetricBuckets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "request_metric_buckets",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    minute_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    route = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status_class = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    count = table.Column<int>(type: "integer", nullable: false),
                    errors = table.Column<int>(type: "integer", nullable: false),
                    latency_sum_ms = table.Column<long>(type: "bigint", nullable: false),
                    lat_le_50 = table.Column<int>(type: "integer", nullable: false),
                    lat_le_200 = table.Column<int>(type: "integer", nullable: false),
                    lat_le_500 = table.Column<int>(type: "integer", nullable: false),
                    lat_le_1500 = table.Column<int>(type: "integer", nullable: false),
                    lat_gt_1500 = table.Column<int>(type: "integer", nullable: false),
                    CreateDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_request_metric_buckets", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_request_metric_buckets_minute",
                table: "request_metric_buckets",
                column: "minute_utc");

            migrationBuilder.CreateIndex(
                name: "ux_request_metric_buckets_minute_route_status",
                table: "request_metric_buckets",
                columns: new[] { "minute_utc", "route", "status_class" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "request_metric_buckets");
        }
    }
}
