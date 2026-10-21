using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace ContractManagement.Tests.Persistence;

public sealed class BilingualSchemaTests
{
    [Fact]
    public void PendingEnglishUpgrade_NormalizesAlreadyAppliedTenants_WithoutChangingData()
    {
        using var db = new DbDtctechContext(new DbContextOptionsBuilder<DbDtctechContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True").Options);
        var assembly = db.GetService<IMigrationsAssembly>();
        var info = assembly.Migrations.Single(x => x.Key.EndsWith("_AllowPendingEnglishMasterFields"));
        var migration = assembly.CreateMigration(info.Value, db.Database.ProviderName!);
        Assert.Equal(6, migration.UpOperations.Count);
        Assert.All(migration.UpOperations, operation =>
        {
            var column = Assert.IsType<AlterColumnOperation>(operation);
            Assert.True(column.IsNullable);
            Assert.Null(column.DefaultValue);
            Assert.Null(column.DefaultValueSql);
            Assert.Contains(column.Table, new[] { "tbl_TenantLegalProfile", "tbl_Products", "tbl_Services" });
        });
        Assert.Empty(migration.DownOperations);
        var sql = db.GetService<IMigrator>().GenerateScript("20261018120857_CatalogItemEnglishNames", info.Key);
        Assert.Contains("ALTER COLUMN [ProductNameEn] nvarchar(500) NULL", sql);
        Assert.Contains("ALTER COLUMN [ServiceNameEn] nvarchar(500) NULL", sql);
        Assert.Contains("ALTER COLUMN [AddressEn] nvarchar(2000) NULL", sql);
        Assert.DoesNotContain("FOREIGN KEY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE [tbl_", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM [tbl_", sql, StringComparison.OrdinalIgnoreCase);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Theory]
    [InlineData("_BilingualContentAndPartyEnglish", typeof(TblTenantLegalProfile), "AddressEn")]
    [InlineData("_CatalogItemEnglishNames", typeof(TblTenantLegalProfile), "LegalEntityNameEn")]
    [InlineData("_CatalogItemEnglishNames", typeof(TblProduct), "ProductNameEn")]
    [InlineData("_CatalogItemEnglishNames", typeof(TblService), "ServiceNameEn")]
    public void RewrittenMigrationTargetModel_MatchesNullableColumns(string suffix, Type entity, string field)
    {
        using var db = new DbDtctechContext(new DbContextOptionsBuilder<DbDtctechContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True").Options);
        var assembly = db.GetService<IMigrationsAssembly>();
        var info = assembly.Migrations.Single(x => x.Key.EndsWith(suffix));
        var migration = assembly.CreateMigration(info.Value, db.Database.ProviderName!);
        Assert.True(migration.TargetModel.FindEntityType(entity.FullName!)!.FindProperty(field)!.IsNullable);
    }

    [Fact]
    public void CatalogEnglishMigration_AllowsPendingTranslations_WithoutDefaultsOrPhysicalForeignKeys()
    {
        using var db = new DbDtctechContext(new DbContextOptionsBuilder<DbDtctechContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True").Options);
        var assembly = db.GetService<IMigrationsAssembly>();
        var info = assembly.Migrations.Single(x => x.Key.EndsWith("_CatalogItemEnglishNames"));
        var migration = assembly.CreateMigration(info.Value, db.Database.ProviderName!);
        Assert.All(migration.UpOperations, operation => Assert.True(operation is AddColumnOperation or AddCheckConstraintOperation));
        var columns = migration.UpOperations.OfType<AddColumnOperation>().ToList();
        Assert.Equal(2, columns.Count);
        Assert.All(columns, column =>
        {
            Assert.True(column.IsNullable);
            Assert.Equal(500, column.MaxLength);
            Assert.Null(column.DefaultValue);
            Assert.Null(column.DefaultValueSql);
        });
        Assert.Equal(2, migration.UpOperations.OfType<AddCheckConstraintOperation>().Count());
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Migration_OnlyAddsColumnsAndChecks_WithoutPhysicalForeignKeysOrFakeEnglishDefaults()
    {
        using var db = new DbDtctechContext(new DbContextOptionsBuilder<DbDtctechContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True").Options);
        var assembly = db.GetService<IMigrationsAssembly>();
        var info = assembly.Migrations.Single(x => x.Key.EndsWith("_BilingualContentAndPartyEnglish"));
        var migration = assembly.CreateMigration(info.Value, db.Database.ProviderName!);
        Assert.All(migration.UpOperations, operation =>
            Assert.True(operation is AddColumnOperation or AddCheckConstraintOperation));
        var english = migration.UpOperations.OfType<AddColumnOperation>()
            .Where(x => x.Table == "tbl_TenantLegalProfile").ToList();
        Assert.Equal(4, english.Count);
        Assert.All(english, col => { Assert.True(col.IsNullable); Assert.Null(col.DefaultValue); });
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
