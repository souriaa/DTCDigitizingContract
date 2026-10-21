using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContractManagement.Infrastructure.Migrations.Application
{
    /// <inheritdoc />
    public partial class ContractPartySnapshotFreezePhase48 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM [tbl_ContractVersionPartySnapshot])
                   OR EXISTS (SELECT 1 FROM [tbl_ContractAcceptanceRecord])
                    THROW 51000, N'ContractPartySnapshotFreezePhase48: snapshot test cũ thiếu dữ liệu liên lạc/ngày ký đã khóa. Cần môi trường dữ liệu mới; không tự backfill hoặc xóa dữ liệu.', 1;
                """);
            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "tbl_ContractVersionPartySnapshot",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactName",
                table: "tbl_ContractVersionPartySnapshot",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPhone",
                table: "tbl_ContractVersionPartySnapshot",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactTitle",
                table: "tbl_ContractVersionPartySnapshot",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "tbl_ContractVersionPartySnapshot",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "tbl_ContractVersionPartySnapshot",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "tbl_ContractVersionPartySnapshot",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Mobile",
                table: "tbl_ContractVersionPartySnapshot",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubjectCode",
                table: "tbl_ContractVersionPartySnapshot",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Website",
                table: "tbl_ContractVersionPartySnapshot",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ZipCode",
                table: "tbl_ContractVersionPartySnapshot",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ContractSignDate",
                table: "tbl_ContractAcceptanceRecord",
                type: "date",
                nullable: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "City",
                table: "tbl_ContractVersionPartySnapshot");

            migrationBuilder.DropColumn(
                name: "ContactName",
                table: "tbl_ContractVersionPartySnapshot");

            migrationBuilder.DropColumn(
                name: "ContactPhone",
                table: "tbl_ContractVersionPartySnapshot");

            migrationBuilder.DropColumn(
                name: "ContactTitle",
                table: "tbl_ContractVersionPartySnapshot");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "tbl_ContractVersionPartySnapshot");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "tbl_ContractVersionPartySnapshot");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "tbl_ContractVersionPartySnapshot");

            migrationBuilder.DropColumn(
                name: "Mobile",
                table: "tbl_ContractVersionPartySnapshot");

            migrationBuilder.DropColumn(
                name: "SubjectCode",
                table: "tbl_ContractVersionPartySnapshot");

            migrationBuilder.DropColumn(
                name: "Website",
                table: "tbl_ContractVersionPartySnapshot");

            migrationBuilder.DropColumn(
                name: "ZipCode",
                table: "tbl_ContractVersionPartySnapshot");

            migrationBuilder.DropColumn(
                name: "ContractSignDate",
                table: "tbl_ContractAcceptanceRecord");
        }
    }
}
