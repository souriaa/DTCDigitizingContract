using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContractManagement.Infrastructure.Migrations.Application
{
    /// <inheritdoc />
    public partial class RemoveContractHandoverTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A finalized acceptance snapshot can include handover allocations in its hash.
            // Never strand a pending signature by deleting its source data silently.
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM [tbl_ContractAcceptanceRecord] AS [record]
                    INNER JOIN [tbl_ContractAcceptanceScopeAllocation] AS [allocation]
                        ON [allocation].[AcceptanceRecordId] = [record].[AcceptanceRecordId]
                    WHERE [record].[Status] = 1
                )
                    THROW 51000, N'Còn biên bản nghiệm thu đã chốt/chờ ký gắn phạm vi bàn giao. Hoàn tất ký bằng phiên bản phần mềm cũ hoặc hủy biên bản qua ứng dụng trước khi chạy migration RemoveContractHandoverTables. Không tự sửa hash hoặc xóa nghiệm thu.', 1;
                """);

            migrationBuilder.DropTable(
                name: "tbl_ContractAcceptanceScopeAllocation");

            migrationBuilder.DropTable(
                name: "tbl_ContractHandoverBaseline");

            migrationBuilder.DropTable(
                name: "tbl_ContractHandoverEvidence");

            migrationBuilder.DropTable(
                name: "tbl_ContractHandoverLine");

            migrationBuilder.DropTable(
                name: "tbl_ContractHandoverParty");

            migrationBuilder.DropTable(
                name: "tbl_ContractHandoverPlaceholderValue");

            migrationBuilder.DropTable(
                name: "tbl_ContractHandoverRecord");

            migrationBuilder.DropTable(
                name: "tbl_ContractHandoverReference");

            migrationBuilder.DropTable(
                name: "tbl_ContractHandoverScopeItem");

            migrationBuilder.DropTable(
                name: "tbl_ContractHandoverSection");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tbl_ContractAcceptanceScopeAllocation",
                columns: table => new
                {
                    AcceptanceScopeAllocationId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AcceptanceRecordId = table.Column<int>(type: "int", nullable: false),
                    HandoverLineId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_ContractAcceptanceScopeAllocation", x => x.AcceptanceScopeAllocationId);
                    table.CheckConstraint("CK_AcceptanceScopeAllocation_Quantity", "[Quantity] > 0");
                });

            migrationBuilder.CreateTable(
                name: "tbl_ContractHandoverBaseline",
                columns: table => new
                {
                    HandoverBaselineId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConfirmedByEmployeeId = table.Column<int>(type: "int", nullable: false),
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    ContractVersionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_ContractHandoverBaseline", x => x.HandoverBaselineId);
                });

            migrationBuilder.CreateTable(
                name: "tbl_ContractHandoverEvidence",
                columns: table => new
                {
                    HandoverEvidenceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FileId = table.Column<int>(type: "int", nullable: false),
                    HandoverRecordId = table.Column<int>(type: "int", nullable: false),
                    SignDate = table.Column<DateTime>(type: "date", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedByEmployeeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_ContractHandoverEvidence", x => x.HandoverEvidenceId);
                });

            migrationBuilder.CreateTable(
                name: "tbl_ContractHandoverLine",
                columns: table => new
                {
                    HandoverLineId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    HandoverRecordId = table.Column<int>(type: "int", nullable: false),
                    HandoverScopeItemId = table.Column<long>(type: "bigint", nullable: false),
                    NotesEn = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    NotesVi = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_ContractHandoverLine", x => x.HandoverLineId);
                    table.CheckConstraint("CK_HandoverLine_Quantity", "[Quantity] > 0");
                });

            migrationBuilder.CreateTable(
                name: "tbl_ContractHandoverParty",
                columns: table => new
                {
                    HandoverPartyId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Address = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AddressEn = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Fax = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HandoverRecordId = table.Column<int>(type: "int", nullable: false),
                    LegalName = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    LegalNameEn = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RepresentativeName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    RepresentativeTitle = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RepresentativeTitleEn = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SubjectKind = table.Column<byte>(type: "tinyint", nullable: false),
                    TaxCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_ContractHandoverParty", x => x.HandoverPartyId);
                    table.CheckConstraint("CK_HandoverParty_Subject", "[SubjectKind] IN (1,2)");
                });

            migrationBuilder.CreateTable(
                name: "tbl_ContractHandoverPlaceholderValue",
                columns: table => new
                {
                    HandoverPlaceholderValueId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HandoverRecordId = table.Column<int>(type: "int", nullable: false),
                    PlaceholderKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RenderedValue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceFieldKey = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_ContractHandoverPlaceholderValue", x => x.HandoverPlaceholderValueId);
                });

            migrationBuilder.CreateTable(
                name: "tbl_ContractHandoverRecord",
                columns: table => new
                {
                    HandoverRecordId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CancellationReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledByEmployeeId = table.Column<int>(type: "int", nullable: true),
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    ContractSignDate = table.Column<DateTime>(type: "date", nullable: false),
                    ContractVersionId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedEmployeeId = table.Column<int>(type: "int", nullable: false),
                    FinalizedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinalizedByEmployeeId = table.Column<int>(type: "int", nullable: true),
                    HandoverBaselineId = table.Column<long>(type: "bigint", nullable: false),
                    HandoverCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    HandoverDate = table.Column<DateTime>(type: "date", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    LocationEn = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    SignedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedByEmployeeId = table.Column<int>(type: "int", nullable: true),
                    SnapshotHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    TemplateVersionId = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedEmployeeId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_ContractHandoverRecord", x => x.HandoverRecordId);
                    table.CheckConstraint("CK_HandoverRecord_Hash", "([SnapshotHash] IS NULL OR LEN([SnapshotHash]) = 64) AND ([Status] NOT IN (1,2) OR [SnapshotHash] IS NOT NULL)");
                    table.CheckConstraint("CK_HandoverRecord_Status", "[Status] IN (0,1,2,3)");
                });

            migrationBuilder.CreateTable(
                name: "tbl_ContractHandoverReference",
                columns: table => new
                {
                    HandoverReferenceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppendixId = table.Column<int>(type: "int", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    HandoverRecordId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_ContractHandoverReference", x => x.HandoverReferenceId);
                });

            migrationBuilder.CreateTable(
                name: "tbl_ContractHandoverScopeItem",
                columns: table => new
                {
                    HandoverScopeItemId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    HandoverBaselineId = table.Column<long>(type: "bigint", nullable: false),
                    ItemCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    NameVi = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RequiredQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    ScopeMode = table.Column<byte>(type: "tinyint", nullable: false),
                    SourceContractItemId = table.Column<int>(type: "int", nullable: false),
                    SourceQuantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UnitVi = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_ContractHandoverScopeItem", x => x.HandoverScopeItemId);
                    table.CheckConstraint("CK_HandoverScope_Mode", "[ScopeMode] IN (1,2)");
                    table.CheckConstraint("CK_HandoverScope_Quantity", "[SourceQuantity] > 0 AND [RequiredQuantity] > 0 AND (([ScopeMode] = 1 AND [RequiredQuantity] = [SourceQuantity]) OR ([ScopeMode] = 2 AND [RequiredQuantity] = 1))");
                });

            migrationBuilder.CreateTable(
                name: "tbl_ContractHandoverSection",
                columns: table => new
                {
                    HandoverSectionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BilingualContentMode = table.Column<byte>(type: "tinyint", nullable: false),
                    ContentEn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContentVi = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    HandoverRecordId = table.Column<int>(type: "int", nullable: false),
                    SectionCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TitleEn = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TitleVi = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tbl_ContractHandoverSection", x => x.HandoverSectionId);
                    table.CheckConstraint("CK_HandoverSection_Mode", "[BilingualContentMode] IN (1,2)");
                });

            migrationBuilder.CreateIndex(
                name: "IX_tbl_ContractAcceptanceScopeAllocation_AcceptanceRecordId_HandoverLineId",
                table: "tbl_ContractAcceptanceScopeAllocation",
                columns: new[] { "AcceptanceRecordId", "HandoverLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_ContractAcceptanceScopeAllocation_HandoverLineId",
                table: "tbl_ContractAcceptanceScopeAllocation",
                column: "HandoverLineId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_ContractHandoverBaseline_ContractId_ContractVersionId",
                table: "tbl_ContractHandoverBaseline",
                columns: new[] { "ContractId", "ContractVersionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_ContractHandoverEvidence_FileId",
                table: "tbl_ContractHandoverEvidence",
                column: "FileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_ContractHandoverEvidence_HandoverRecordId",
                table: "tbl_ContractHandoverEvidence",
                column: "HandoverRecordId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_ContractHandoverLine_HandoverRecordId_DisplayOrder",
                table: "tbl_ContractHandoverLine",
                columns: new[] { "HandoverRecordId", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_ContractHandoverLine_HandoverRecordId_HandoverScopeItemId",
                table: "tbl_ContractHandoverLine",
                columns: new[] { "HandoverRecordId", "HandoverScopeItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_ContractHandoverLine_HandoverScopeItemId",
                table: "tbl_ContractHandoverLine",
                column: "HandoverScopeItemId");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_ContractHandoverParty_HandoverRecordId_SubjectKind",
                table: "tbl_ContractHandoverParty",
                columns: new[] { "HandoverRecordId", "SubjectKind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_ContractHandoverPlaceholderValue_HandoverRecordId_PlaceholderKey",
                table: "tbl_ContractHandoverPlaceholderValue",
                columns: new[] { "HandoverRecordId", "PlaceholderKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_ContractHandoverRecord_ContractId_HandoverCode",
                table: "tbl_ContractHandoverRecord",
                columns: new[] { "ContractId", "HandoverCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_ContractHandoverRecord_ContractVersionId_Status",
                table: "tbl_ContractHandoverRecord",
                columns: new[] { "ContractVersionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_HandoverRecord_ActiveVersion",
                table: "tbl_ContractHandoverRecord",
                columns: new[] { "ContractId", "ContractVersionId" },
                unique: true,
                filter: "[Status] IN (0,1)");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_ContractHandoverReference_HandoverRecordId_DisplayOrder",
                table: "tbl_ContractHandoverReference",
                columns: new[] { "HandoverRecordId", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_ContractHandoverScopeItem_HandoverBaselineId_DisplayOrder",
                table: "tbl_ContractHandoverScopeItem",
                columns: new[] { "HandoverBaselineId", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_ContractHandoverScopeItem_HandoverBaselineId_SourceContractItemId",
                table: "tbl_ContractHandoverScopeItem",
                columns: new[] { "HandoverBaselineId", "SourceContractItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_ContractHandoverSection_HandoverRecordId_DisplayOrder",
                table: "tbl_ContractHandoverSection",
                columns: new[] { "HandoverRecordId", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tbl_ContractHandoverSection_HandoverRecordId_SectionCode",
                table: "tbl_ContractHandoverSection",
                columns: new[] { "HandoverRecordId", "SectionCode" },
                unique: true);
        }
    }
}
