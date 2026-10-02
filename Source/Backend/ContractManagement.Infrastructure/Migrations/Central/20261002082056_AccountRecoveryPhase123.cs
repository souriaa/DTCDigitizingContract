using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContractManagement.Infrastructure.Migrations.Central
{
    /// <inheritdoc />
    public partial class AccountRecoveryPhase123 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RecoveryEmailVerifiedAt",
                table: "SystemAdmins",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AccountRecoveryChallenges",
                columns: table => new
                {
                    RecoveryChallengeId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SystemAdminId = table.Column<int>(type: "int", nullable: false),
                    Purpose = table.Column<byte>(type: "tinyint", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    EmailHash = table.Column<byte[]>(type: "binary(32)", nullable: false),
                    SessionVersionAtIssue = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestCount = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountRecoveryChallenges", x => x.RecoveryChallengeId);
                    table.CheckConstraint("CK_AccountRecoveryChallenges_Account", "[SystemAdminId] > 0 AND [SessionVersionAtIssue] > 0 AND [RequestCount] > 0");
                    table.CheckConstraint("CK_AccountRecoveryChallenges_Dates", "[ExpiresAtUtc] > [CreatedAtUtc] AND [LastRequestedAtUtc] >= [CreatedAtUtc]");
                    table.CheckConstraint("CK_AccountRecoveryChallenges_Purpose", "[Purpose] IN (1, 2)");
                });

            migrationBuilder.CreateTable(
                name: "AccountRecoveryEmailOutbox",
                columns: table => new
                {
                    OutboxId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecoveryChallengeId = table.Column<long>(type: "bigint", nullable: true),
                    SystemAdminId = table.Column<int>(type: "int", nullable: false),
                    TemplateKind = table.Column<byte>(type: "tinyint", nullable: false),
                    RecipientCiphertext = table.Column<byte[]>(type: "varbinary(1024)", maxLength: 1024, nullable: true),
                    TokenCiphertext = table.Column<byte[]>(type: "varbinary(512)", maxLength: 512, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LeaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LeaseUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureCode = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountRecoveryEmailOutbox", x => x.OutboxId);
                    table.CheckConstraint("CK_AccountRecoveryEmailOutbox_Lease", "([Status] = 2 AND [LeaseId] IS NOT NULL AND [LeaseUntilUtc] IS NOT NULL) OR ([Status] <> 2 AND [LeaseId] IS NULL AND [LeaseUntilUtc] IS NULL)");
                    table.CheckConstraint("CK_AccountRecoveryEmailOutbox_Values", "[SystemAdminId] > 0 AND [TemplateKind] IN (1, 2, 3) AND [Status] IN (1, 2, 3, 4) AND [AttemptCount] >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountRecoveryChallenges_PublicId",
                table: "AccountRecoveryChallenges",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountRecoveryChallenges_SystemAdminId_CreatedAtUtc",
                table: "AccountRecoveryChallenges",
                columns: new[] { "SystemAdminId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountRecoveryChallenges_SystemAdminId_Purpose",
                table: "AccountRecoveryChallenges",
                columns: new[] { "SystemAdminId", "Purpose" },
                unique: true,
                filter: "[UsedAtUtc] IS NULL AND [RevokedAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AccountRecoveryEmailOutbox_RecoveryChallengeId",
                table: "AccountRecoveryEmailOutbox",
                column: "RecoveryChallengeId",
                unique: true,
                filter: "[RecoveryChallengeId] IS NOT NULL AND [Status] IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_AccountRecoveryEmailOutbox_Status_NextAttemptAtUtc",
                table: "AccountRecoveryEmailOutbox",
                columns: new[] { "Status", "NextAttemptAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountRecoveryChallenges");

            migrationBuilder.DropTable(
                name: "AccountRecoveryEmailOutbox");

            migrationBuilder.DropColumn(
                name: "RecoveryEmailVerifiedAt",
                table: "SystemAdmins");
        }
    }
}
