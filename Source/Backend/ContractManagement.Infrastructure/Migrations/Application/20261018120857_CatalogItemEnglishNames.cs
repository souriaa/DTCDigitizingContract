using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContractManagement.Infrastructure.Migrations.Application
{
    /// <inheritdoc />
    public partial class CatalogItemEnglishNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ServiceNameEn",
                table: "tbl_Services",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductNameEn",
                table: "tbl_Products",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Services_ServiceNameEn",
                table: "tbl_Services",
                sql: "LEN(LTRIM(RTRIM([ServiceNameEn]))) > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_ProductNameEn",
                table: "tbl_Products",
                sql: "LEN(LTRIM(RTRIM([ProductNameEn]))) > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Services_ServiceNameEn",
                table: "tbl_Services");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_ProductNameEn",
                table: "tbl_Products");

            migrationBuilder.DropColumn(
                name: "ServiceNameEn",
                table: "tbl_Services");

            migrationBuilder.DropColumn(
                name: "ProductNameEn",
                table: "tbl_Products");
        }
    }
}
