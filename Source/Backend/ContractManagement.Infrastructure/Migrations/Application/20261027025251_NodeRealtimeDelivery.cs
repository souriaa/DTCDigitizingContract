using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContractManagement.Infrastructure.Migrations.Application
{
    /// <inheritdoc />
    public partial class NodeRealtimeDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NodeAttemptCount",
                table: "tbl_NotificationDispatchOutbox",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "NodeDispatchedAtUtc",
                table: "tbl_NotificationDispatchOutbox",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NodeLastErrorCode",
                table: "tbl_NotificationDispatchOutbox",
                type: "varchar(80)",
                unicode: false,
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "NodeLeaseId",
                table: "tbl_NotificationDispatchOutbox",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NodeLeaseUntilUtc",
                table: "tbl_NotificationDispatchOutbox",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NodeNextAttemptAtUtc",
                table: "tbl_NotificationDispatchOutbox",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NodeStatus",
                table: "tbl_NotificationDispatchOutbox",
                type: "varchar(16)",
                unicode: false,
                maxLength: 16,
                nullable: false,
                defaultValue: "Disabled");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_NotificationDispatchOutbox_NodeStatus_NodeNextAttemptAtUtc",
                table: "tbl_NotificationDispatchOutbox",
                columns: new[] { "NodeStatus", "NodeNextAttemptAtUtc" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_tbl_NotificationDispatchOutbox_NodeAttemptCount",
                table: "tbl_NotificationDispatchOutbox",
                sql: "[NodeAttemptCount] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_tbl_NotificationDispatchOutbox_NodeStatus",
                table: "tbl_NotificationDispatchOutbox",
                sql: "[NodeStatus] IN ('Disabled', 'Pending', 'Leased', 'Dispatched', 'Failed')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tbl_NotificationDispatchOutbox_NodeStatus_NodeNextAttemptAtUtc",
                table: "tbl_NotificationDispatchOutbox");

            migrationBuilder.DropCheckConstraint(
                name: "CK_tbl_NotificationDispatchOutbox_NodeAttemptCount",
                table: "tbl_NotificationDispatchOutbox");

            migrationBuilder.DropCheckConstraint(
                name: "CK_tbl_NotificationDispatchOutbox_NodeStatus",
                table: "tbl_NotificationDispatchOutbox");

            migrationBuilder.DropColumn(
                name: "NodeAttemptCount",
                table: "tbl_NotificationDispatchOutbox");

            migrationBuilder.DropColumn(
                name: "NodeDispatchedAtUtc",
                table: "tbl_NotificationDispatchOutbox");

            migrationBuilder.DropColumn(
                name: "NodeLastErrorCode",
                table: "tbl_NotificationDispatchOutbox");

            migrationBuilder.DropColumn(
                name: "NodeLeaseId",
                table: "tbl_NotificationDispatchOutbox");

            migrationBuilder.DropColumn(
                name: "NodeLeaseUntilUtc",
                table: "tbl_NotificationDispatchOutbox");

            migrationBuilder.DropColumn(
                name: "NodeNextAttemptAtUtc",
                table: "tbl_NotificationDispatchOutbox");

            migrationBuilder.DropColumn(
                name: "NodeStatus",
                table: "tbl_NotificationDispatchOutbox");
        }
    }
}
