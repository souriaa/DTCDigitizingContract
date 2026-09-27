using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContractManagement.Infrastructure.Migrations.Application;

/// <summary>Test data is disposable: replace the unused legacy table in one migration.</summary>
public partial class NotificationInboxAndDispatch : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "tbl_Notification");
        migrationBuilder.CreateTable(
            name: "tbl_Notification",
            columns: table => new
            {
                NotificationId = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                EventKey = table.Column<string>(type: "varchar(200)", unicode: false, maxLength: 200, nullable: false),
                ModuleCode = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                EventCode = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: false),
                SubjectType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                SubjectId = table.Column<int>(type: "int", nullable: false),
                ActorEmployeeId = table.Column<int>(type: "int", nullable: true),
                Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                Body = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                ActionKey = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tbl_Notification", x => x.NotificationId);
                table.ForeignKey("FK_tbl_Notification_tbl_Employee_ActorEmployeeId",
                    x => x.ActorEmployeeId, "tbl_Employee", "EmployeeId");
            });
        migrationBuilder.CreateIndex("IX_tbl_Notification_EventKey", "tbl_Notification", "EventKey", unique: true);
        migrationBuilder.CreateIndex("IX_tbl_Notification_ActorEmployeeId", "tbl_Notification", "ActorEmployeeId");

        migrationBuilder.CreateTable(
            name: "tbl_NotificationRecipient",
            columns: table => new
            {
                NotificationRecipientId = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                NotificationId = table.Column<long>(type: "bigint", nullable: false),
                EmployeeId = table.Column<int>(type: "int", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                ReadAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tbl_NotificationRecipient", x => x.NotificationRecipientId);
                table.ForeignKey("FK_tbl_NotificationRecipient_tbl_Notification_NotificationId",
                    x => x.NotificationId, "tbl_Notification", "NotificationId", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_tbl_NotificationRecipient_tbl_Employee_EmployeeId",
                    x => x.EmployeeId, "tbl_Employee", "EmployeeId");
            });
        migrationBuilder.CreateIndex("IX_tbl_NotificationRecipient_NotificationId_EmployeeId",
            "tbl_NotificationRecipient", new[] { "NotificationId", "EmployeeId" }, unique: true);
        migrationBuilder.CreateIndex("IX_tbl_NotificationRecipient_EmployeeId_CreatedAtUtc_NotificationRecipientId",
            "tbl_NotificationRecipient", new[] { "EmployeeId", "CreatedAtUtc", "NotificationRecipientId" },
            descending: new[] { false, true, true });
        migrationBuilder.CreateIndex("IX_tbl_NotificationRecipient_EmployeeId_ReadAtUtc",
            "tbl_NotificationRecipient", new[] { "EmployeeId", "ReadAtUtc" });

        migrationBuilder.CreateTable(
            name: "tbl_NotificationDispatchOutbox",
            columns: table => new
            {
                OutboxId = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                NotificationId = table.Column<long>(type: "bigint", nullable: false),
                Status = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                AttemptCount = table.Column<int>(type: "int", nullable: false),
                NextAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                LeaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                LeaseUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                DispatchedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastErrorCode = table.Column<string>(type: "varchar(80)", unicode: false, maxLength: 80, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_tbl_NotificationDispatchOutbox", x => x.OutboxId);
                table.ForeignKey("FK_tbl_NotificationDispatchOutbox_tbl_Notification_NotificationId",
                    x => x.NotificationId, "tbl_Notification", "NotificationId", onDelete: ReferentialAction.Cascade);
            });
        migrationBuilder.CreateIndex("IX_tbl_NotificationDispatchOutbox_NotificationId",
            "tbl_NotificationDispatchOutbox", "NotificationId", unique: true);
        migrationBuilder.CreateIndex("IX_tbl_NotificationDispatchOutbox_Status_NextAttemptAtUtc",
            "tbl_NotificationDispatchOutbox", new[] { "Status", "NextAttemptAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "tbl_NotificationDispatchOutbox");
        migrationBuilder.DropTable(name: "tbl_NotificationRecipient");
        migrationBuilder.DropTable(name: "tbl_Notification");
        migrationBuilder.CreateTable(
            name: "tbl_Notification",
            columns: table => new
            {
                NotificationId = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                UserId = table.Column<int>(type: "int", nullable: false),
                NotificationTitle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                NotificationMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ObjectType = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: true),
                ObjectId = table.Column<int>(type: "int", nullable: true),
                IsRead = table.Column<bool>(type: "bit", nullable: false),
                CreatedDate = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
            },
            constraints: table => table.PrimaryKey("PK__tbl_Noti__20CF2E127E71675E", x => x.NotificationId));
    }
}
