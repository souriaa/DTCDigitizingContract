using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContractManagement.Infrastructure.Migrations.Application
{
    /// <inheritdoc />
    public partial class FixContractAuditAppendixAcceptanceSubjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_tbl_ContractAudit_Subject",
                table: "tbl_ContractAudit");

            migrationBuilder.AddCheckConstraint(
                name: "CK_tbl_ContractAudit_Subject",
                table: "tbl_ContractAudit",
                sql: "([SubjectType] IS NULL AND [SubjectId] IS NULL) OR ([SubjectType] IN ('Contract', 'ContractVersion', 'NegotiationComment', 'CustomerAccessLink', 'CustomerOtpChallenge', 'CustomerAccessSession', 'ApprovalRequest', 'SignedEvidence', 'ContractAppendix', 'AcceptanceRecord', 'AcceptanceEvidence', 'Payment') AND [SubjectId] > 0)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_tbl_ContractAudit_Subject",
                table: "tbl_ContractAudit");

            migrationBuilder.AddCheckConstraint(
                name: "CK_tbl_ContractAudit_Subject",
                table: "tbl_ContractAudit",
                sql: "([SubjectType] IS NULL AND [SubjectId] IS NULL) OR ([SubjectType] IN ('Contract', 'ContractVersion', 'NegotiationComment', 'CustomerAccessLink', 'CustomerOtpChallenge', 'CustomerAccessSession', 'ApprovalRequest', 'SignedEvidence', 'AcceptanceEvidence', 'Payment') AND [SubjectId] > 0)");
        }
    }
}
