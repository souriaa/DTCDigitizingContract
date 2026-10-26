using ContractManagement.Infrastructure.Persistence.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace ContractManagement.Tests.Persistence;

public sealed class RestoredRbacSchemaTests
{
    [Fact]
    public void ReviewMigration_PreservesHandoverAndData_AndMatchesCurrentModel()
    {
        using var db = new DbDtctechContext(new DbContextOptionsBuilder<DbDtctechContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True").Options);
        Assert.False(db.Database.HasPendingModelChanges());
        var names = db.Model.GetEntityTypes().Select(x => x.GetTableName()).ToArray();
        Assert.Contains("tbl_ContractHandoverRecord", names);
        Assert.Contains("tbl_ContractHandoverScopeItem", names);
        Assert.Contains("tbl_ContractAcceptanceScopeAllocation", names);
        var assembly = db.GetService<IMigrationsAssembly>();
        Assert.DoesNotContain(assembly.Migrations.Keys, x => x.Contains("RemoveContractHandover") || x.Contains("RetireHandover"));
        var info = assembly.Migrations.Single(x => x.Key.EndsWith("_RestoreAdministrativeReviewRbac"));
        var migration = assembly.CreateMigration(info.Value, db.Database.ProviderName!);
        Assert.Equal(3, migration.UpOperations.OfType<AddColumnOperation>().Count());
        Assert.Empty(migration.UpOperations.OfType<DropTableOperation>());
        Assert.Empty(migration.UpOperations.OfType<AddForeignKeyOperation>());
        var sql = db.GetService<IMigrator>().GenerateScript("20261021045743_AcceptanceEvidenceSignDate", info.Key);
        Assert.DoesNotContain("DELETE FROM", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP TABLE", sql, StringComparison.OrdinalIgnoreCase);
    }
}
