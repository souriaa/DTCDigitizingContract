using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContractManagement.Infrastructure.Migrations.Application;

public partial class RestoreAdministrativeReviewRbac : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint("CK_tbl_Contract_Status", "tbl_Contract");
        migrationBuilder.DropCheckConstraint("CK_tbl_ContractApprovalRequest_Status", "tbl_ContractApprovalRequest");
        migrationBuilder.DropIndex("UX_tbl_ContractApprovalRequest_PendingContract", "tbl_ContractApprovalRequest");
        migrationBuilder.AddColumn<int>("AdministrativeReviewerEmployeeId", "tbl_ContractApprovalRequest", type: "int", nullable: true);
        migrationBuilder.AddColumn<DateTime>("AdministrativeReviewedAt", "tbl_ContractApprovalRequest", type: "datetime2", nullable: true);
        migrationBuilder.AddColumn<string>("AdministrativeReviewComment", "tbl_ContractApprovalRequest", type: "nvarchar(1000)", maxLength: 1000, nullable: true);
        migrationBuilder.AddCheckConstraint("CK_tbl_Contract_Status", "tbl_Contract", "[Status] IN (0, 1, 2, 3, 4, 5, 6, 7, 8)");
        migrationBuilder.AddCheckConstraint("CK_tbl_ContractApprovalRequest_Status", "tbl_ContractApprovalRequest", "[Status] IN (0, 1, 2, 3, 4, 5)");
        migrationBuilder.CreateIndex("UX_tbl_ContractApprovalRequest_PendingContract", "tbl_ContractApprovalRequest", "ContractId", unique: true, filter: "[Status] IN (0, 5)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("IF EXISTS (SELECT 1 FROM [tbl_Contract] WHERE [Status] = 8) OR EXISTS (SELECT 1 FROM [tbl_ContractApprovalRequest] WHERE [Status] = 5 OR [AdministrativeReviewerEmployeeId] IS NOT NULL) THROW 51000, N'Không thể hạ schema khi đã có hồ sơ kiểm tra hành chính.', 1;");
        migrationBuilder.DropIndex("UX_tbl_ContractApprovalRequest_PendingContract", "tbl_ContractApprovalRequest");
        migrationBuilder.DropCheckConstraint("CK_tbl_Contract_Status", "tbl_Contract");
        migrationBuilder.DropCheckConstraint("CK_tbl_ContractApprovalRequest_Status", "tbl_ContractApprovalRequest");
        migrationBuilder.DropColumn("AdministrativeReviewerEmployeeId", "tbl_ContractApprovalRequest");
        migrationBuilder.DropColumn("AdministrativeReviewedAt", "tbl_ContractApprovalRequest");
        migrationBuilder.DropColumn("AdministrativeReviewComment", "tbl_ContractApprovalRequest");
        migrationBuilder.AddCheckConstraint("CK_tbl_Contract_Status", "tbl_Contract", "[Status] IN (0, 1, 2, 3, 4, 5, 6, 7)");
        migrationBuilder.AddCheckConstraint("CK_tbl_ContractApprovalRequest_Status", "tbl_ContractApprovalRequest", "[Status] IN (0, 1, 2, 3, 4)");
        migrationBuilder.CreateIndex("UX_tbl_ContractApprovalRequest_PendingContract", "tbl_ContractApprovalRequest", "ContractId", unique: true, filter: "[Status] = 0");
    }
}
