using ContractManagement.Infrastructure.Persistence.Application;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContractManagement.Infrastructure.Migrations.Application;

[DbContext(typeof(DbDtctechContext))]
[Migration("20260928090000_UseUnicodeContractAttachmentFileNames")]
public sealed class UseUnicodeContractAttachmentFileNames : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "ContractFileName",
            table: "tbl_ContractAttachment",
            type: "nvarchar(300)",
            maxLength: 300,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(300)",
            oldUnicode: false,
            oldMaxLength: 300,
            oldNullable: true);

        // The original Unicode name is still available in FileStorage for
        // attachments created before this migration.
        migrationBuilder.Sql("""
            UPDATE attachment
            SET ContractFileName = storage.FileName
            FROM dbo.tbl_ContractAttachment AS attachment
            INNER JOIN dbo.tbl_FileStorage AS storage
                ON storage.FilePath = attachment.ContractFilePath
                AND storage.ObjectType = 'Contract'
                AND storage.ObjectId = attachment.ContractId
            WHERE attachment.ContractFileName IS NOT NULL
                AND attachment.ContractFileName <> storage.FileName;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (
                SELECT 1
                FROM dbo.tbl_ContractAttachment
                WHERE ContractFileName IS NOT NULL
                    AND ContractFileName <> CONVERT(nvarchar(300), CONVERT(varchar(300), ContractFileName))
            )
                THROW 51001, 'Cannot revert Unicode attachment names without data loss.', 1;
            """);

        migrationBuilder.AlterColumn<string>(
            name: "ContractFileName",
            table: "tbl_ContractAttachment",
            type: "varchar(300)",
            unicode: false,
            maxLength: 300,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(300)",
            oldMaxLength: 300,
            oldNullable: true);
    }
}
