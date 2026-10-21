using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContractManagement.Infrastructure.Migrations.Application
{
    /// <inheritdoc />
    public partial class CatalogItemUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UnitName",
                table: "tbl_Services",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnitNameEn",
                table: "tbl_Services",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnitName",
                table: "tbl_Products",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnitNameEn",
                table: "tbl_Products",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnitName",
                table: "tbl_Services");

            migrationBuilder.DropColumn(
                name: "UnitNameEn",
                table: "tbl_Services");

            migrationBuilder.DropColumn(
                name: "UnitName",
                table: "tbl_Products");

            migrationBuilder.DropColumn(
                name: "UnitNameEn",
                table: "tbl_Products");
        }
    }
}
