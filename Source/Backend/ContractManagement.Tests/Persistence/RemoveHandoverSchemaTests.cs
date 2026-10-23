using ContractManagement.Infrastructure.Persistence.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace ContractManagement.Tests.Persistence;

public sealed class RemoveHandoverSchemaTests
{
    private static readonly string[] RemovedTables =
    [
        "tbl_ContractAcceptanceScopeAllocation", "tbl_ContractHandoverBaseline",
        "tbl_ContractHandoverEvidence", "tbl_ContractHandoverLine", "tbl_ContractHandoverParty",
        "tbl_ContractHandoverPlaceholderValue", "tbl_ContractHandoverRecord",
        "tbl_ContractHandoverReference", "tbl_ContractHandoverScopeItem", "tbl_ContractHandoverSection"
    ];

    private static DbDtctechContext ModelContext() => new(new DbContextOptionsBuilder<DbDtctechContext>()
        .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True").Options);

    [Fact]
    public void Model_NoLongerMapsHandoverTables_AndSnapshotMatches()
    {
        using var db = ModelContext();
        var names = db.Model.GetEntityTypes().Select(x => x.GetTableName()).ToHashSet();
        Assert.All(RemovedTables, name => Assert.DoesNotContain(name, names));
        foreach (var name in new[] { "tbl_Contract", "tbl_ContractVersion", "tbl_ContractAcceptanceRecord",
            "tbl_ContractAcceptanceEvidence", "tbl_ContractPaymentLedger", "tbl_ContractAudit", "tbl_FileStorage" })
            Assert.Contains(name, names);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Migration_DropsExactlyTenTables_WithoutChangingContractAcceptancePaymentOrAudit()
    {
        using var db = ModelContext();
        var assembly = db.GetService<IMigrationsAssembly>();
        var info = assembly.Migrations.Single(x => x.Key.EndsWith("_RemoveContractHandoverTables"));
        var migration = assembly.CreateMigration(info.Value, db.Database.ProviderName!);
        var guard = Assert.IsType<SqlOperation>(migration.UpOperations[0]);
        Assert.Contains("[record].[Status] = 1", guard.Sql);
        Assert.Contains("THROW 51000", guard.Sql);
        Assert.Equal(RemovedTables.Order(), migration.UpOperations.OfType<DropTableOperation>().Select(x => x.Name).Order());
        Assert.Equal(11, migration.UpOperations.Count);
        Assert.All(migration.UpOperations.Skip(1), operation => Assert.IsType<DropTableOperation>(operation));
        var sql = db.GetService<IMigrator>().GenerateScript("20261021045743_AcceptanceEvidenceSignDate", info.Key);
        Assert.DoesNotContain("DELETE FROM", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE [tbl_", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ALTER TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FOREIGN KEY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(RemovedTables.Order(), migration.DownOperations.OfType<CreateTableOperation>().Select(x => x.Name).Order());
        Assert.All(migration.DownOperations.OfType<CreateTableOperation>(), table => Assert.Empty(table.ForeignKeys));
    }
}
