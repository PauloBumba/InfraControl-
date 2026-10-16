using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InfraControl.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVirtualMachineLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "hypervisor_device_id",
                table: "devices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "vcenter_device_id",
                table: "devices",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_devices_hypervisor_device_id",
                table: "devices",
                column: "hypervisor_device_id");

            migrationBuilder.CreateIndex(
                name: "IX_devices_vcenter_device_id",
                table: "devices",
                column: "vcenter_device_id");

            migrationBuilder.AddForeignKey(
                name: "FK_devices_devices_hypervisor_device_id",
                table: "devices",
                column: "hypervisor_device_id",
                principalTable: "devices",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_devices_devices_vcenter_device_id",
                table: "devices",
                column: "vcenter_device_id",
                principalTable: "devices",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_devices_devices_hypervisor_device_id",
                table: "devices");

            migrationBuilder.DropForeignKey(
                name: "FK_devices_devices_vcenter_device_id",
                table: "devices");

            migrationBuilder.DropIndex(
                name: "IX_devices_hypervisor_device_id",
                table: "devices");

            migrationBuilder.DropIndex(
                name: "IX_devices_vcenter_device_id",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "hypervisor_device_id",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "vcenter_device_id",
                table: "devices");
        }
    }
}
