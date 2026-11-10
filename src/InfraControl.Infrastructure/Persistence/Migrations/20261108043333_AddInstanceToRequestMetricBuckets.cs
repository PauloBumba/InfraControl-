using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInstanceToRequestMetricBuckets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_request_metric_buckets_minute_route_status",
                table: "request_metric_buckets");

            migrationBuilder.AddColumn<string>(
                name: "instance",
                table: "request_metric_buckets",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ux_request_metric_buckets_minute_route_status_instance",
                table: "request_metric_buckets",
                columns: new[] { "minute_utc", "route", "status_class", "instance" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_request_metric_buckets_minute_route_status_instance",
                table: "request_metric_buckets");

            migrationBuilder.DropColumn(
                name: "instance",
                table: "request_metric_buckets");

            migrationBuilder.CreateIndex(
                name: "ux_request_metric_buckets_minute_route_status",
                table: "request_metric_buckets",
                columns: new[] { "minute_utc", "route", "status_class" },
                unique: true);
        }
    }
}
