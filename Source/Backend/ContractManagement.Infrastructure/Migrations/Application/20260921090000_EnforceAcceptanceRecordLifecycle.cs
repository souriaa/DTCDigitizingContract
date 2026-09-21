using ContractManagement.Infrastructure.Persistence.Application;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContractManagement.Infrastructure.Migrations.Application;

[DbContext(typeof(DbDtctechContext))]
[Migration("20260921090000_EnforceAcceptanceRecordLifecycle")]
public partial class EnforceAcceptanceRecordLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "UX_tbl_ContractAcceptanceRecord_ActiveVersion",
            table: "tbl_ContractAcceptanceRecord",
            column: "ContractVersionId",
            unique: true,
            filter: "[Status] IN (0, 1)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "UX_tbl_ContractAcceptanceRecord_ActiveVersion",
            table: "tbl_ContractAcceptanceRecord");
    }
}
