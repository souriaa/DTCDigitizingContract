using ContractManagement.Infrastructure.Migrations.Application;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace ContractManagement.Tests.Integration;

public sealed class UseUnicodeContractAttachmentFileNamesMigrationTests
{
    [Fact]
    public void Model_MapsAttachmentFileNameToUnicode()
    {
        using var context = new DbDtctechContext(
            new DbContextOptionsBuilder<DbDtctechContext>()
                .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True")
                .Options);

        var property = context.Model.FindEntityType(typeof(TblContractAttachment))!
            .FindProperty(nameof(TblContractAttachment.ContractFileName))!;

        Assert.Equal("nvarchar(300)", property.GetRelationalTypeMapping().StoreType);
        Assert.Equal(300, property.GetMaxLength());
    }

    [Fact]
    public void Up_ChangesOnlyFileNameTypeAndRepairsNamesFromStorage()
    {
        var operations = new UseUnicodeContractAttachmentFileNames()
            .UpOperations;
        var alteration = Assert.Single(operations.OfType<AlterColumnOperation>());
        Assert.Equal("tbl_ContractAttachment", alteration.Table);
        Assert.Equal("ContractFileName", alteration.Name);
        Assert.Equal("nvarchar(300)", alteration.ColumnType);
        Assert.Equal("varchar(300)", alteration.OldColumn.ColumnType);
        Assert.Single(operations.OfType<SqlOperation>());
        Assert.Equal(2, operations.Count);
    }
}
