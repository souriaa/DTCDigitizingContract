using ContractManagement.Infrastructure.Persistence.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
namespace ContractManagement.Tests.Persistence;
public sealed class HandoverSchemaTests
{
    [Fact]
    public void Migration_OnlyAddsRelationalTablesAndExpandsAudit_NoForeignKeysJsonOrDataBackfill()
    {
        using var db = new DbDtctechContext(new DbContextOptionsBuilder<DbDtctechContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True").Options);
        var assembly = db.GetService<IMigrationsAssembly>(); var info = assembly.Migrations.Single(x => x.Key.EndsWith("_HandoverFoundationPhase03"));
        var migration = assembly.CreateMigration(info.Value, db.Database.ProviderName!);
        var tables = migration.UpOperations.OfType<CreateTableOperation>().ToArray();
        Assert.Equal(10, tables.Length);
        Assert.All(tables, t => {
            Assert.Empty(t.ForeignKeys);
            Assert.All(t.Columns, x => { Assert.DoesNotContain("Json", x.Name); Assert.Null(x.DefaultValueSql); Assert.Null(x.DefaultValue); });
        });
        Assert.Empty(migration.UpOperations.OfType<DropColumnOperation>());
        Assert.Empty(migration.UpOperations.OfType<AddForeignKeyOperation>());
        Assert.Contains(migration.UpOperations.OfType<CreateIndexOperation>(), x => x.Name == "UX_HandoverRecord_ActiveVersion" && x.IsUnique && x.Filter == "[Status] IN (0,1)");
        var sql = db.GetService<IMigrator>().GenerateScript("20261019081136_ContractPartySnapshotFreezePhase48", info.Key);
        Assert.DoesNotContain("FOREIGN KEY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("INSERT INTO", sql.Split("INSERT INTO [__EFMigrationsHistory]")[0], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE [tbl_", sql, StringComparison.OrdinalIgnoreCase);
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
