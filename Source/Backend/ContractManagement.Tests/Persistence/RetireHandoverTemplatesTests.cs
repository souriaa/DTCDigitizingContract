using System.Text.RegularExpressions;
using ContractManagement.Common.Enums;
using ContractManagement.Domains.Policies.ContractTemplate;
using ContractManagement.Infrastructure.Persistence.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace ContractManagement.Tests.Persistence;

public sealed class RetireHandoverTemplatesTests
{
    [Fact]
    public void RetiredDocumentTypes_AreNotSelectableOrSupported()
    {
        Assert.False(Enum.IsDefined((TemplateDocumentType)4));
        Assert.False(Enum.IsDefined((DocumentType)2));
        Assert.Throws<ArgumentOutOfRangeException>(() => ContractTemplatePolicy.GetOutputKind((TemplateDocumentType)4));
        Assert.False(SupportingDocumentCatalog.Supports((TemplateDocumentType)4));
        Assert.True(SupportingDocumentCatalog.Supports(TemplateDocumentType.AcceptanceRecord));
    }

    [Fact]
    public void Migration_DeletesOnlyRetiredTemplateAggregate_AndPreservesAuditFilesAndCoreData()
    {
        using var db = new DbDtctechContext(new DbContextOptionsBuilder<DbDtctechContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True").Options);
        var assembly = db.GetService<IMigrationsAssembly>();
        var info = assembly.Migrations.Single(x => x.Key.EndsWith("_RetireHandoverTemplates"));
        var migration = assembly.CreateMigration(info.Value, db.Database.ProviderName!);
        Assert.Equal(3, migration.UpOperations.Count);
        Assert.IsType<SqlOperation>(migration.UpOperations[0]);
        var cleanup = ((SqlOperation)migration.UpOperations[0]).Sql;
        Assert.Contains("WHERE [DocumentType] = 4", cleanup);
        Assert.Contains("THROW 51000", cleanup);
        Assert.True(cleanup.IndexOf("THROW 51000", StringComparison.Ordinal) < cleanup.IndexOf("DELETE FROM", StringComparison.Ordinal));
        var tables = Regex.Matches(cleanup, @"DELETE FROM \[(\w+)\]").Select(x => x.Groups[1].Value).ToArray();
        Assert.Equal(9, tables.Length);
        Assert.All(tables, table => Assert.StartsWith("tbl_ContractTemplate", table));
        Assert.DoesNotContain("tbl_ContractTemplateAudit", tables);
        Assert.DoesNotContain("tbl_ContractTemplateAuditValue", tables);
        Assert.DoesNotContain("tbl_FileStorage", tables);
        var check = Assert.IsType<AddCheckConstraintOperation>(migration.UpOperations[2]);
        Assert.Equal("[DocumentType] IN (1, 2, 3, 5, 6, 7, 8, 99)", check.Sql);
        Assert.False(db.Database.HasPendingModelChanges());
        var script = db.GetService<IMigrator>().GenerateScript("20261023090305_RemoveContractHandoverTables", info.Key);
        Assert.DoesNotContain("FOREIGN KEY", script, StringComparison.OrdinalIgnoreCase);
    }
}
