using ContractManagement.Infrastructure.Persistence.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace ContractManagement.Tests.Persistence;

public sealed class AcceptanceSignDateMigrationTests
{
    [Fact]
    public void Migration_AddsOnlyDateColumnWithoutBackfillDefaultsOrForeignKeys()
    {
        using var db = new DbDtctechContext(new DbContextOptionsBuilder<DbDtctechContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True").Options);
        var assembly = db.GetService<IMigrationsAssembly>();
        var type = assembly.Migrations.Single(x => x.Key.EndsWith("_AcceptanceEvidenceSignDate")).Value;
        var migration = assembly.CreateMigration(type, db.Database.ProviderName!);
        var column = Assert.IsType<AddColumnOperation>(Assert.Single(migration.UpOperations));
        Assert.Equal("tbl_ContractAcceptanceEvidence", column.Table);
        Assert.Equal("SignDate", column.Name);
        Assert.Equal("date", column.ColumnType);
        Assert.True(column.IsNullable);
        Assert.Null(column.DefaultValue); Assert.Null(column.DefaultValueSql);
        Assert.IsType<DropColumnOperation>(Assert.Single(migration.DownOperations));
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
