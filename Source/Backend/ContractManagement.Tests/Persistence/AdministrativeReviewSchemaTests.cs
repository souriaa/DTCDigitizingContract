using ContractManagement.Infrastructure.Persistence.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace ContractManagement.Tests.Persistence;

public sealed class AdministrativeReviewSchemaTests
{
    [Fact]
    public void Migration_AddsReviewFieldsAndPendingUniqueness_WithoutResettingDataOrAddingForeignKeys()
    {
        using var db = new DbDtctechContext(new DbContextOptionsBuilder<DbDtctechContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True").Options);
        var assembly = db.GetService<IMigrationsAssembly>();
        var info = assembly.Migrations.Single(x => x.Key.EndsWith("_ContractAdministrativeReviewRbac"));
        var migration = assembly.CreateMigration(info.Value, db.Database.ProviderName!);
        var columns = migration.UpOperations.OfType<AddColumnOperation>().ToArray();
        Assert.Equal(3, columns.Length);
        Assert.All(columns, x => { Assert.True(x.IsNullable); Assert.Equal("tbl_ContractApprovalRequest", x.Table); });
        var pendingIndex = Assert.Single(migration.UpOperations.OfType<CreateIndexOperation>());
        Assert.True(pendingIndex.IsUnique);
        Assert.Equal("[Status] IN (0, 5)", pendingIndex.Filter);
        Assert.False(db.Database.HasPendingModelChanges());
        var sql = db.GetService<IMigrator>().GenerateScript("20261023092950_RetireHandoverTemplates", info.Key);
        foreach (var forbidden in new[] { "FOREIGN KEY", "DELETE FROM", "UPDATE [", "DROP TABLE" })
            Assert.DoesNotContain(forbidden, sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AdministrativeReviewerEmployeeId", sql);
        Assert.Contains("[Status] IN (0, 1, 2, 3, 4, 5, 6, 7, 8)", sql);
    }
}
