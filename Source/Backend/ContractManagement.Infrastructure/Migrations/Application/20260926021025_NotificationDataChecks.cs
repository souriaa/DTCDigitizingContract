using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContractManagement.Infrastructure.Migrations.Application
{
    /// <inheritdoc />
    public partial class NotificationDataChecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_tbl_NotificationDispatchOutbox_AttemptCount",
                table: "tbl_NotificationDispatchOutbox",
                sql: "[AttemptCount] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_tbl_NotificationDispatchOutbox_Status",
                table: "tbl_NotificationDispatchOutbox",
                sql: "[Status] IN ('Pending', 'Leased', 'Dispatched', 'Failed')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_tbl_Notification_EventKey",
                table: "tbl_Notification",
                sql: "LEN(LTRIM(RTRIM([EventKey]))) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_tbl_Notification_SubjectId",
                table: "tbl_Notification",
                sql: "[SubjectId] > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_tbl_NotificationDispatchOutbox_AttemptCount",
                table: "tbl_NotificationDispatchOutbox");

            migrationBuilder.DropCheckConstraint(
                name: "CK_tbl_NotificationDispatchOutbox_Status",
                table: "tbl_NotificationDispatchOutbox");

            migrationBuilder.DropCheckConstraint(
                name: "CK_tbl_Notification_EventKey",
                table: "tbl_Notification");

            migrationBuilder.DropCheckConstraint(
                name: "CK_tbl_Notification_SubjectId",
                table: "tbl_Notification");
        }
    }
}
