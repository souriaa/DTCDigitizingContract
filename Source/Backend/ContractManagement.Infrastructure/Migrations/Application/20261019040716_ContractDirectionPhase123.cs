using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContractManagement.Infrastructure.Migrations.Application
{
    /// <inheritdoc />
    public partial class ContractDirectionPhase123 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [tbl_Contract])
                   OR EXISTS (SELECT 1 FROM [tbl_ContractTemplateVersion])
                   OR EXISTS (SELECT 1 FROM [tbl_ContractVersionLegalSnapshot])
                    THROW 51000, N'ContractDirectionPhase123: dữ liệu test cũ chưa có chiều Mua/Bán. Dừng migration; không tự gán Bán hoặc xóa dữ liệu. Cần môi trường dữ liệu mới cho schema này.', 1;
                """);
            migrationBuilder.AddColumn<byte>(
                name: "ContractDirection",
                table: "tbl_ContractVersionLegalSnapshot",
                type: "tinyint",
                nullable: false);

            migrationBuilder.AddColumn<byte>(
                name: "ContractDirection",
                table: "tbl_ContractTemplateVersion",
                type: "tinyint",
                nullable: false);

            migrationBuilder.AddColumn<byte>(
                name: "ContractDirection",
                table: "tbl_Contract",
                type: "tinyint",
                nullable: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_tbl_ContractVersionLegalSnapshot_ContractDirection",
                table: "tbl_ContractVersionLegalSnapshot",
                sql: "[ContractDirection] IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_ContractTemplateVersion_ContractDirection_Status",
                table: "tbl_ContractTemplateVersion",
                columns: new[] { "ContractDirection", "Status" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_tbl_ContractTemplateVersion_ContractDirection",
                table: "tbl_ContractTemplateVersion",
                sql: "[ContractDirection] IN (1, 2)");

            migrationBuilder.CreateIndex(
                name: "IX_tbl_Contract_ContractDirection_Status",
                table: "tbl_Contract",
                columns: new[] { "ContractDirection", "Status" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_tbl_Contract_ContractDirection",
                table: "tbl_Contract",
                sql: "[ContractDirection] IN (1, 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_tbl_ContractVersionLegalSnapshot_ContractDirection",
                table: "tbl_ContractVersionLegalSnapshot");

            migrationBuilder.DropIndex(
                name: "IX_tbl_ContractTemplateVersion_ContractDirection_Status",
                table: "tbl_ContractTemplateVersion");

            migrationBuilder.DropCheckConstraint(
                name: "CK_tbl_ContractTemplateVersion_ContractDirection",
                table: "tbl_ContractTemplateVersion");

            migrationBuilder.DropIndex(
                name: "IX_tbl_Contract_ContractDirection_Status",
                table: "tbl_Contract");

            migrationBuilder.DropCheckConstraint(
                name: "CK_tbl_Contract_ContractDirection",
                table: "tbl_Contract");

            migrationBuilder.DropColumn(
                name: "ContractDirection",
                table: "tbl_ContractVersionLegalSnapshot");

            migrationBuilder.DropColumn(
                name: "ContractDirection",
                table: "tbl_ContractTemplateVersion");

            migrationBuilder.DropColumn(
                name: "ContractDirection",
                table: "tbl_Contract");
        }
    }
}
