using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace ContractManagement.Tests.Persistence;

public sealed class ContractDirectionSchemaTests
{
    [Fact]
    public void PartyFreezeMigration_HasNoDefaultsBackfillOrPhysicalForeignKeys()
    {
        using var db = new DbDtctechContext(new DbContextOptionsBuilder<DbDtctechContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True").Options);
        var assembly = db.GetService<IMigrationsAssembly>();
        var info = assembly.Migrations.Single(x => x.Key.EndsWith("_ContractPartySnapshotFreezePhase48"));
        var migration = assembly.CreateMigration(info.Value, db.Database.ProviderName!);
        Assert.Contains("THROW 51000", Assert.IsType<SqlOperation>(migration.UpOperations[0]).Sql);
        Assert.Empty(migration.UpOperations.OfType<AddForeignKeyOperation>());
        var columns = migration.UpOperations.OfType<AddColumnOperation>().ToList();
        Assert.Equal(12, columns.Count);
        Assert.All(columns, x => { Assert.Null(x.DefaultValue); Assert.Null(x.DefaultValueSql); Assert.DoesNotContain("Json", x.Name); });
        Assert.Contains(columns, x => x.Name == "ContactName");
        Assert.Contains(columns, x => x.Name == "ContractSignDate" && !x.IsNullable);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Migration_RequiresNewData_NoGuessedDefaults_NoJson_NoForeignKeys()
    {
        using var db = new DbDtctechContext(new DbContextOptionsBuilder<DbDtctechContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True").Options);
        var assembly = db.GetService<IMigrationsAssembly>();
        var info = assembly.Migrations.Single(x => x.Key.EndsWith("_ContractDirectionPhase123"));
        var migration = assembly.CreateMigration(info.Value, db.Database.ProviderName!);
        var precondition = Assert.IsType<SqlOperation>(migration.UpOperations[0]);
        Assert.Contains("THROW 51000", precondition.Sql);
        Assert.DoesNotContain("DELETE", precondition.Sql, StringComparison.OrdinalIgnoreCase);
        var columns = migration.UpOperations.OfType<AddColumnOperation>().ToList();
        Assert.Equal(3, columns.Count);
        Assert.All(columns, x =>
        {
            Assert.Equal("ContractDirection", x.Name);
            Assert.Equal("tinyint", x.ColumnType);
            Assert.False(x.IsNullable);
            Assert.Null(x.DefaultValue);
            Assert.Null(x.DefaultValueSql);
        });
        Assert.Equal(3, migration.UpOperations.OfType<AddCheckConstraintOperation>().Count());
        Assert.Empty(migration.UpOperations.OfType<AddForeignKeyOperation>());
        var sql = db.GetService<IMigrator>().GenerateScript("20261018144616_AllowPendingEnglishMasterFields", info.Key);
        Assert.DoesNotContain("FOREIGN KEY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE [tbl_", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DEFAULT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.False(db.Database.HasPendingModelChanges());
        foreach (var entity in new[] { typeof(TblContract), typeof(TblContractTemplateVersion), typeof(TblContractVersionLegalSnapshot) })
            Assert.False(db.Model.FindEntityType(entity)!.FindProperty("ContractDirection")!.IsNullable);
    }
}
