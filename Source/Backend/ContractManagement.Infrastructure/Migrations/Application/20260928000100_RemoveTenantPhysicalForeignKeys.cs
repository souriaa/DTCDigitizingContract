using ContractManagement.Infrastructure.Persistence.Application;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContractManagement.Infrastructure.Migrations.Application;

/// <summary>
/// Removes only physical tenant FK constraints. Scalar ID columns, indexes and
/// EF relationships remain so existing tracked graph writes keep their behavior.
/// </summary>
[DbContext(typeof(DbDtctechContext))]
[Migration("20260928000100_RemoveTenantPhysicalForeignKeys")]
public sealed class RemoveTenantPhysicalForeignKeys : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF (SELECT COUNT(*) FROM sys.foreign_keys fk
                JOIN sys.tables t ON t.object_id = fk.parent_object_id
                WHERE t.name LIKE 'tbl[_]%') <> 58
                THROW 51000, 'Tenant FK baseline differs from the reviewed 58 constraints.', 1;
            """);

        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractAcceptanceEvidence_tbl_ContractAcceptanceRecord_AcceptanceRecordId", table: "tbl_ContractAcceptanceEvidence");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractAcceptanceEvidence_tbl_Employee_UploadedByEmployeeId", table: "tbl_ContractAcceptanceEvidence");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractAcceptanceEvidence_tbl_FileStorage_FileId", table: "tbl_ContractAcceptanceEvidence");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractAcceptanceMilestone_tbl_ContractAcceptanceRecord_AcceptanceRecordId", table: "tbl_ContractAcceptanceMilestone");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractAcceptanceMilestone_tbl_ContractPaymentMilestone_PaymentMilestoneId", table: "tbl_ContractAcceptanceMilestone");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractAcceptanceParty_tbl_ContractAcceptanceRecord_AcceptanceRecordId", table: "tbl_ContractAcceptanceParty");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractAcceptanceRecord_tbl_Contract_ContractId", table: "tbl_ContractAcceptanceRecord");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractAcceptanceRecord_tbl_ContractTemplateVersion_TemplateVersionId", table: "tbl_ContractAcceptanceRecord");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractAcceptanceRecord_tbl_ContractVersion_ContractVersionId", table: "tbl_ContractAcceptanceRecord");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractAcceptanceRecord_tbl_Employee_CancelledByEmployeeId", table: "tbl_ContractAcceptanceRecord");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractAcceptanceRecord_tbl_Employee_CreatedEmployeeId", table: "tbl_ContractAcceptanceRecord");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractAcceptanceRecord_tbl_Employee_FinalizedByEmployeeId", table: "tbl_ContractAcceptanceRecord");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractAcceptanceRecord_tbl_Employee_SignedByEmployeeId", table: "tbl_ContractAcceptanceRecord");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractAcceptanceRecord_tbl_Employee_UpdatedEmployeeId", table: "tbl_ContractAcceptanceRecord");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractAcceptanceReference_tbl_ContractAcceptanceRecord_AcceptanceRecordId", table: "tbl_ContractAcceptanceReference");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractAcceptanceReference_tbl_ContractAppendix_AppendixId", table: "tbl_ContractAcceptanceReference");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractAcceptanceSection_tbl_ContractAcceptanceRecord_AcceptanceRecordId", table: "tbl_ContractAcceptanceSection");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractAppendix_tbl_Contract_ContractId", table: "tbl_ContractAppendix");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractAppendix_tbl_ContractTemplateAppendix_SourceTemplateAppendixId", table: "tbl_ContractAppendix");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractAppendix_tbl_ContractVersion_VersionId", table: "tbl_ContractAppendix");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractAppendix_tbl_Employee_CreatedEmployeeId", table: "tbl_ContractAppendix");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractAppendix_tbl_Employee_UpdatedEmployeeId", table: "tbl_ContractAppendix");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractAppendixTerm_tbl_ContractAppendix_AppendixId", table: "tbl_ContractAppendixTerm");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractAppendixTerm_tbl_ContractTemplateAppendixTerm_SourceTemplateAppendixTermId", table: "tbl_ContractAppendixTerm");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractAuditValue_tbl_ContractAudit_ContractAuditId", table: "tbl_ContractAuditValue");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractPaymentLedger_tbl_Contract_ContractId", table: "tbl_ContractPaymentLedger");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractPaymentLedger_tbl_ContractPaymentMilestone_PaymentMilestoneId", table: "tbl_ContractPaymentLedger");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractPaymentLedger_tbl_ContractVersion_VersionId", table: "tbl_ContractPaymentLedger");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractPaymentLedger_tbl_Employee_CreatedByEmployeeId", table: "tbl_ContractPaymentLedger");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractPaymentLedger_tbl_Employee_VoidedByEmployeeId", table: "tbl_ContractPaymentLedger");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractPaymentLedger_tbl_FileStorage_EvidenceFileId", table: "tbl_ContractPaymentLedger");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractPaymentMilestone_tbl_Contract_ContractId", table: "tbl_ContractPaymentMilestone");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractPaymentMilestone_tbl_ContractTerm_TermId", table: "tbl_ContractPaymentMilestone");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractPaymentMilestone_tbl_ContractVersion_VersionId", table: "tbl_ContractPaymentMilestone");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractPaymentMilestone_tbl_Employee_PaidByEmployeeId", table: "tbl_ContractPaymentMilestone");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractSignedEvidence_tbl_Contract_ContractId", table: "tbl_ContractSignedEvidence");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractSignedEvidence_tbl_ContractSignedEvidence_SupersedesEvidenceId", table: "tbl_ContractSignedEvidence");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractSignedEvidence_tbl_ContractVersion_VersionId", table: "tbl_ContractSignedEvidence");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractSignedEvidence_tbl_Employee_SupersededByEmployeeId", table: "tbl_ContractSignedEvidence");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractSignedEvidence_tbl_Employee_UploadedByEmployeeId", table: "tbl_ContractSignedEvidence");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractSignedEvidence_tbl_FileStorage_FileId", table: "tbl_ContractSignedEvidence");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractTemplateAppendix_tbl_ContractTemplateVersion_TemplateVersionId", table: "tbl_ContractTemplateAppendix");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractTemplateAppendix_tbl_Employee_CreatedEmployeeId", table: "tbl_ContractTemplateAppendix");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractTemplateAppendix_tbl_Employee_UpdatedEmployeeId", table: "tbl_ContractTemplateAppendix");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractTemplateAppendixTerm_tbl_ContractTemplateAppendix_TemplateAppendixId", table: "tbl_ContractTemplateAppendixTerm");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractTemplateAppendixTerm_tbl_Employee_CreatedEmployeeId", table: "tbl_ContractTemplateAppendixTerm");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractTemplateAppendixTerm_tbl_Employee_UpdatedEmployeeId", table: "tbl_ContractTemplateAppendixTerm");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractTemplateAuditValue_tbl_ContractTemplateAudit_ContractTemplateAuditId", table: "tbl_ContractTemplateAuditValue");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractTemplateItemTableColumnLayout_tbl_ContractTemplateVersion_TemplateVersionId", table: "tbl_ContractTemplateItemTableColumnLayout");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractTemplatePaymentMilestone_tbl_ContractTemplateTerm_TemplateTermId", table: "tbl_ContractTemplatePaymentMilestone");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractTemplatePaymentMilestone_tbl_ContractTemplateVersion_TemplateVersionId", table: "tbl_ContractTemplatePaymentMilestone");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractVersionLegalSnapshot_tbl_ContractVersion_VersionId", table: "tbl_ContractVersionLegalSnapshot");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractVersionPartySnapshot_tbl_ContractVersionLegalSnapshot_ContractVersionLegalSnapshotId", table: "tbl_ContractVersionPartySnapshot");
        migrationBuilder.DropForeignKey(name: "FK_tbl_ContractVersionPaymentMilestoneSnapshot_tbl_ContractVersionLegalSnapshot_ContractVersionLegalSnapshotId", table: "tbl_ContractVersionPaymentMilestoneSnapshot");
        migrationBuilder.DropForeignKey(name: "FK_tbl_Notification_tbl_Employee_ActorEmployeeId", table: "tbl_Notification");
        migrationBuilder.DropForeignKey(name: "FK_tbl_NotificationDispatchOutbox_tbl_Notification_NotificationId", table: "tbl_NotificationDispatchOutbox");
        migrationBuilder.DropForeignKey(name: "FK_tbl_NotificationRecipient_tbl_Employee_EmployeeId", table: "tbl_NotificationRecipient");
        migrationBuilder.DropForeignKey(name: "FK_tbl_NotificationRecipient_tbl_Notification_NotificationId", table: "tbl_NotificationRecipient");

        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM sys.foreign_keys fk
                JOIN sys.tables t ON t.object_id = fk.parent_object_id
                WHERE t.name LIKE 'tbl[_]%')
                THROW 51001, 'Tenant still has a physical foreign key.', 1;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // This intentionally fails if rows became orphaned after Up().
        migrationBuilder.AddForeignKey(name: "FK_tbl_NotificationRecipient_tbl_Notification_NotificationId", table: "tbl_NotificationRecipient", column: "NotificationId", principalTable: "tbl_Notification", principalColumn: "NotificationId", onDelete: ReferentialAction.Cascade);
        migrationBuilder.AddForeignKey(name: "FK_tbl_NotificationRecipient_tbl_Employee_EmployeeId", table: "tbl_NotificationRecipient", column: "EmployeeId", principalTable: "tbl_Employee", principalColumn: "EmployeeId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_NotificationDispatchOutbox_tbl_Notification_NotificationId", table: "tbl_NotificationDispatchOutbox", column: "NotificationId", principalTable: "tbl_Notification", principalColumn: "NotificationId", onDelete: ReferentialAction.Cascade);
        migrationBuilder.AddForeignKey(name: "FK_tbl_Notification_tbl_Employee_ActorEmployeeId", table: "tbl_Notification", column: "ActorEmployeeId", principalTable: "tbl_Employee", principalColumn: "EmployeeId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractVersionPaymentMilestoneSnapshot_tbl_ContractVersionLegalSnapshot_ContractVersionLegalSnapshotId", table: "tbl_ContractVersionPaymentMilestoneSnapshot", column: "ContractVersionLegalSnapshotId", principalTable: "tbl_ContractVersionLegalSnapshot", principalColumn: "ContractVersionLegalSnapshotId", onDelete: ReferentialAction.Cascade);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractVersionPartySnapshot_tbl_ContractVersionLegalSnapshot_ContractVersionLegalSnapshotId", table: "tbl_ContractVersionPartySnapshot", column: "ContractVersionLegalSnapshotId", principalTable: "tbl_ContractVersionLegalSnapshot", principalColumn: "ContractVersionLegalSnapshotId", onDelete: ReferentialAction.Cascade);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractVersionLegalSnapshot_tbl_ContractVersion_VersionId", table: "tbl_ContractVersionLegalSnapshot", column: "VersionId", principalTable: "tbl_ContractVersion", principalColumn: "VersionId", onDelete: ReferentialAction.Cascade);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractTemplatePaymentMilestone_tbl_ContractTemplateVersion_TemplateVersionId", table: "tbl_ContractTemplatePaymentMilestone", column: "TemplateVersionId", principalTable: "tbl_ContractTemplateVersion", principalColumn: "TemplateVersionId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractTemplatePaymentMilestone_tbl_ContractTemplateTerm_TemplateTermId", table: "tbl_ContractTemplatePaymentMilestone", column: "TemplateTermId", principalTable: "tbl_ContractTemplateTerm", principalColumn: "TemplateTermId", onDelete: ReferentialAction.Cascade);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractTemplateItemTableColumnLayout_tbl_ContractTemplateVersion_TemplateVersionId", table: "tbl_ContractTemplateItemTableColumnLayout", column: "TemplateVersionId", principalTable: "tbl_ContractTemplateVersion", principalColumn: "TemplateVersionId", onDelete: ReferentialAction.Cascade);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractTemplateAuditValue_tbl_ContractTemplateAudit_ContractTemplateAuditId", table: "tbl_ContractTemplateAuditValue", column: "ContractTemplateAuditId", principalTable: "tbl_ContractTemplateAudit", principalColumn: "ContractTemplateAuditId", onDelete: ReferentialAction.Cascade);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractTemplateAppendixTerm_tbl_Employee_UpdatedEmployeeId", table: "tbl_ContractTemplateAppendixTerm", column: "UpdatedEmployeeId", principalTable: "tbl_Employee", principalColumn: "EmployeeId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractTemplateAppendixTerm_tbl_Employee_CreatedEmployeeId", table: "tbl_ContractTemplateAppendixTerm", column: "CreatedEmployeeId", principalTable: "tbl_Employee", principalColumn: "EmployeeId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractTemplateAppendixTerm_tbl_ContractTemplateAppendix_TemplateAppendixId", table: "tbl_ContractTemplateAppendixTerm", column: "TemplateAppendixId", principalTable: "tbl_ContractTemplateAppendix", principalColumn: "TemplateAppendixId", onDelete: ReferentialAction.Cascade);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractTemplateAppendix_tbl_Employee_UpdatedEmployeeId", table: "tbl_ContractTemplateAppendix", column: "UpdatedEmployeeId", principalTable: "tbl_Employee", principalColumn: "EmployeeId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractTemplateAppendix_tbl_Employee_CreatedEmployeeId", table: "tbl_ContractTemplateAppendix", column: "CreatedEmployeeId", principalTable: "tbl_Employee", principalColumn: "EmployeeId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractTemplateAppendix_tbl_ContractTemplateVersion_TemplateVersionId", table: "tbl_ContractTemplateAppendix", column: "TemplateVersionId", principalTable: "tbl_ContractTemplateVersion", principalColumn: "TemplateVersionId", onDelete: ReferentialAction.Cascade);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractSignedEvidence_tbl_FileStorage_FileId", table: "tbl_ContractSignedEvidence", column: "FileId", principalTable: "tbl_FileStorage", principalColumn: "FileId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractSignedEvidence_tbl_Employee_UploadedByEmployeeId", table: "tbl_ContractSignedEvidence", column: "UploadedByEmployeeId", principalTable: "tbl_Employee", principalColumn: "EmployeeId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractSignedEvidence_tbl_Employee_SupersededByEmployeeId", table: "tbl_ContractSignedEvidence", column: "SupersededByEmployeeId", principalTable: "tbl_Employee", principalColumn: "EmployeeId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractSignedEvidence_tbl_ContractVersion_VersionId", table: "tbl_ContractSignedEvidence", column: "VersionId", principalTable: "tbl_ContractVersion", principalColumn: "VersionId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractSignedEvidence_tbl_ContractSignedEvidence_SupersedesEvidenceId", table: "tbl_ContractSignedEvidence", column: "SupersedesEvidenceId", principalTable: "tbl_ContractSignedEvidence", principalColumn: "SignedEvidenceId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractSignedEvidence_tbl_Contract_ContractId", table: "tbl_ContractSignedEvidence", column: "ContractId", principalTable: "tbl_Contract", principalColumn: "ContractId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractPaymentMilestone_tbl_Employee_PaidByEmployeeId", table: "tbl_ContractPaymentMilestone", column: "PaidByEmployeeId", principalTable: "tbl_Employee", principalColumn: "EmployeeId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractPaymentMilestone_tbl_ContractVersion_VersionId", table: "tbl_ContractPaymentMilestone", column: "VersionId", principalTable: "tbl_ContractVersion", principalColumn: "VersionId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractPaymentMilestone_tbl_ContractTerm_TermId", table: "tbl_ContractPaymentMilestone", column: "TermId", principalTable: "tbl_ContractTerm", principalColumn: "TermId", onDelete: ReferentialAction.Cascade);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractPaymentMilestone_tbl_Contract_ContractId", table: "tbl_ContractPaymentMilestone", column: "ContractId", principalTable: "tbl_Contract", principalColumn: "ContractId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractPaymentLedger_tbl_FileStorage_EvidenceFileId", table: "tbl_ContractPaymentLedger", column: "EvidenceFileId", principalTable: "tbl_FileStorage", principalColumn: "FileId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractPaymentLedger_tbl_Employee_VoidedByEmployeeId", table: "tbl_ContractPaymentLedger", column: "VoidedByEmployeeId", principalTable: "tbl_Employee", principalColumn: "EmployeeId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractPaymentLedger_tbl_Employee_CreatedByEmployeeId", table: "tbl_ContractPaymentLedger", column: "CreatedByEmployeeId", principalTable: "tbl_Employee", principalColumn: "EmployeeId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractPaymentLedger_tbl_ContractVersion_VersionId", table: "tbl_ContractPaymentLedger", column: "VersionId", principalTable: "tbl_ContractVersion", principalColumn: "VersionId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractPaymentLedger_tbl_ContractPaymentMilestone_PaymentMilestoneId", table: "tbl_ContractPaymentLedger", column: "PaymentMilestoneId", principalTable: "tbl_ContractPaymentMilestone", principalColumn: "PaymentMilestoneId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractPaymentLedger_tbl_Contract_ContractId", table: "tbl_ContractPaymentLedger", column: "ContractId", principalTable: "tbl_Contract", principalColumn: "ContractId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractAuditValue_tbl_ContractAudit_ContractAuditId", table: "tbl_ContractAuditValue", column: "ContractAuditId", principalTable: "tbl_ContractAudit", principalColumn: "ContractAuditId", onDelete: ReferentialAction.Cascade);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractAppendixTerm_tbl_ContractTemplateAppendixTerm_SourceTemplateAppendixTermId", table: "tbl_ContractAppendixTerm", column: "SourceTemplateAppendixTermId", principalTable: "tbl_ContractTemplateAppendixTerm", principalColumn: "TemplateAppendixTermId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractAppendixTerm_tbl_ContractAppendix_AppendixId", table: "tbl_ContractAppendixTerm", column: "AppendixId", principalTable: "tbl_ContractAppendix", principalColumn: "AppendixId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractAppendix_tbl_Employee_UpdatedEmployeeId", table: "tbl_ContractAppendix", column: "UpdatedEmployeeId", principalTable: "tbl_Employee", principalColumn: "EmployeeId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractAppendix_tbl_Employee_CreatedEmployeeId", table: "tbl_ContractAppendix", column: "CreatedEmployeeId", principalTable: "tbl_Employee", principalColumn: "EmployeeId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractAppendix_tbl_ContractVersion_VersionId", table: "tbl_ContractAppendix", column: "VersionId", principalTable: "tbl_ContractVersion", principalColumn: "VersionId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractAppendix_tbl_ContractTemplateAppendix_SourceTemplateAppendixId", table: "tbl_ContractAppendix", column: "SourceTemplateAppendixId", principalTable: "tbl_ContractTemplateAppendix", principalColumn: "TemplateAppendixId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractAppendix_tbl_Contract_ContractId", table: "tbl_ContractAppendix", column: "ContractId", principalTable: "tbl_Contract", principalColumn: "ContractId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractAcceptanceSection_tbl_ContractAcceptanceRecord_AcceptanceRecordId", table: "tbl_ContractAcceptanceSection", column: "AcceptanceRecordId", principalTable: "tbl_ContractAcceptanceRecord", principalColumn: "AcceptanceRecordId", onDelete: ReferentialAction.Cascade);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractAcceptanceReference_tbl_ContractAppendix_AppendixId", table: "tbl_ContractAcceptanceReference", column: "AppendixId", principalTable: "tbl_ContractAppendix", principalColumn: "AppendixId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractAcceptanceReference_tbl_ContractAcceptanceRecord_AcceptanceRecordId", table: "tbl_ContractAcceptanceReference", column: "AcceptanceRecordId", principalTable: "tbl_ContractAcceptanceRecord", principalColumn: "AcceptanceRecordId", onDelete: ReferentialAction.Cascade);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractAcceptanceRecord_tbl_Employee_UpdatedEmployeeId", table: "tbl_ContractAcceptanceRecord", column: "UpdatedEmployeeId", principalTable: "tbl_Employee", principalColumn: "EmployeeId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractAcceptanceRecord_tbl_Employee_SignedByEmployeeId", table: "tbl_ContractAcceptanceRecord", column: "SignedByEmployeeId", principalTable: "tbl_Employee", principalColumn: "EmployeeId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractAcceptanceRecord_tbl_Employee_FinalizedByEmployeeId", table: "tbl_ContractAcceptanceRecord", column: "FinalizedByEmployeeId", principalTable: "tbl_Employee", principalColumn: "EmployeeId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractAcceptanceRecord_tbl_Employee_CreatedEmployeeId", table: "tbl_ContractAcceptanceRecord", column: "CreatedEmployeeId", principalTable: "tbl_Employee", principalColumn: "EmployeeId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractAcceptanceRecord_tbl_Employee_CancelledByEmployeeId", table: "tbl_ContractAcceptanceRecord", column: "CancelledByEmployeeId", principalTable: "tbl_Employee", principalColumn: "EmployeeId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractAcceptanceRecord_tbl_ContractVersion_ContractVersionId", table: "tbl_ContractAcceptanceRecord", column: "ContractVersionId", principalTable: "tbl_ContractVersion", principalColumn: "VersionId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractAcceptanceRecord_tbl_ContractTemplateVersion_TemplateVersionId", table: "tbl_ContractAcceptanceRecord", column: "TemplateVersionId", principalTable: "tbl_ContractTemplateVersion", principalColumn: "TemplateVersionId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractAcceptanceRecord_tbl_Contract_ContractId", table: "tbl_ContractAcceptanceRecord", column: "ContractId", principalTable: "tbl_Contract", principalColumn: "ContractId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractAcceptanceParty_tbl_ContractAcceptanceRecord_AcceptanceRecordId", table: "tbl_ContractAcceptanceParty", column: "AcceptanceRecordId", principalTable: "tbl_ContractAcceptanceRecord", principalColumn: "AcceptanceRecordId", onDelete: ReferentialAction.Cascade);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractAcceptanceMilestone_tbl_ContractPaymentMilestone_PaymentMilestoneId", table: "tbl_ContractAcceptanceMilestone", column: "PaymentMilestoneId", principalTable: "tbl_ContractPaymentMilestone", principalColumn: "PaymentMilestoneId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractAcceptanceMilestone_tbl_ContractAcceptanceRecord_AcceptanceRecordId", table: "tbl_ContractAcceptanceMilestone", column: "AcceptanceRecordId", principalTable: "tbl_ContractAcceptanceRecord", principalColumn: "AcceptanceRecordId", onDelete: ReferentialAction.Cascade);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractAcceptanceEvidence_tbl_FileStorage_FileId", table: "tbl_ContractAcceptanceEvidence", column: "FileId", principalTable: "tbl_FileStorage", principalColumn: "FileId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractAcceptanceEvidence_tbl_Employee_UploadedByEmployeeId", table: "tbl_ContractAcceptanceEvidence", column: "UploadedByEmployeeId", principalTable: "tbl_Employee", principalColumn: "EmployeeId", onDelete: ReferentialAction.NoAction);
        migrationBuilder.AddForeignKey(name: "FK_tbl_ContractAcceptanceEvidence_tbl_ContractAcceptanceRecord_AcceptanceRecordId", table: "tbl_ContractAcceptanceEvidence", column: "AcceptanceRecordId", principalTable: "tbl_ContractAcceptanceRecord", principalColumn: "AcceptanceRecordId", onDelete: ReferentialAction.NoAction);
    }
}

