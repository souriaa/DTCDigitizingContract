using System.Text.RegularExpressions;
using ContractManagement.Infrastructure.Persistence.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ContractManagement.Tests.Persistence;

public sealed class SqlSeedArtifactsTests
{
    private static DbDtctechContext ModelContext() => new(new DbContextOptionsBuilder<DbDtctechContext>()
        .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True").Options);

    private static string Read(string name)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "Artifacts", "Sql", name);
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException("Cannot locate repository SQL seed artifact", name);
    }

    [Theory]
    [InlineData("seed-test-departments.sql", 1)]
    [InlineData("seed-rbac-test-employees.sql", 1)]
    [InlineData("seed-test-customers.sql", 1)]
    [InlineData("seed-test-catalog.sql", 5)]
    public void SeedInsertColumns_MatchCurrentModel_AndSupplyRequiredValues(string file, int expectedInserts)
    {
        using var db = ModelContext();
        var model = db.GetService<IDesignTimeModel>().Model;
        var inserts = Regex.Matches(Read(file), @"\bINSERT\s+INTO\s+dbo\.(?<table>tbl_\w+)\s*\((?<columns>[^)]+)\)", RegexOptions.IgnoreCase);
        Assert.Equal(expectedInserts, inserts.Count);
        foreach (Match insert in inserts)
        {
            var table = insert.Groups["table"].Value;
            var entity = Assert.Single(model.GetEntityTypes(), x => x.GetTableName() == table);
            var store = StoreObjectIdentifier.Create(entity, StoreObjectType.Table)!.Value;
            var properties = entity.GetProperties().ToDictionary(x => x.GetColumnName(store)!, StringComparer.OrdinalIgnoreCase);
            var columns = insert.Groups["columns"].Value.Split(',').Select(x => x.Trim().Trim('[', ']')).ToArray();
            Assert.Equal(columns.Length, columns.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            foreach (var column in columns)
            {
                Assert.True(properties.ContainsKey(column), $"{file}: {table}.{column} is not in current EF model");
                Assert.False(properties[column].IsConcurrencyToken, $"{file}: do not insert SQL rowversion");
            }
            foreach (var property in properties.Values.Where(x => !x.IsNullable && x.ValueGenerated == ValueGenerated.Never
                && x.GetDefaultValueSql() is null && x.FindAnnotation(RelationalAnnotationNames.DefaultValue) is null))
                Assert.Contains(property.GetColumnName(store)!, columns, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("seed-test-departments.sql")]
    [InlineData("seed-rbac-test-employees.sql")]
    [InlineData("seed-test-customers.sql")]
    [InlineData("seed-test-catalog.sql")]
    public void Seeds_RequireExplicitMigratedTenant_AndNeverResetSchemaOrIdentities(string file)
    {
        var sql = Read(file);
        using var db = ModelContext();
        var latestMigration = db.GetService<IMigrationsAssembly>().Migrations.Keys.OrderBy(x => x, StringComparer.Ordinal).Last();
        Assert.Contains(latestMigration, sql);
        // The operator may configure a real tenant name without invalidating schema tests.
        Assert.Matches(@"@ExpectedDatabase\s+sysname\s*=\s*N'[^']+'", sql);
        Assert.Contains("DB_NAME() <> @ExpectedDatabase", sql);
        Assert.Contains("SET XACT_ABORT ON", sql);
        Assert.Contains("BEGIN TRANSACTION", sql);
        Assert.Contains("ROLLBACK TRANSACTION", sql);
        Assert.Contains("NOT EXISTS", sql);
        Assert.DoesNotContain("IDENTITY_INSERT", sql);
        Assert.DoesNotMatch(@"(?im)^\s*(USE|CREATE|ALTER|DROP|TRUNCATE|DELETE|MERGE)\b", sql);
        Assert.Contains("UPDLOCK, HOLDLOCK", sql);
    }

    [Fact]
    public void CustomerSeed_UsesNewIdentityCodesAndLogicalParents_WithoutOldPasswordsOrLookupIds()
    {
        var sql = Read("seed-test-customers.sql");
        Assert.Contains("N'CUST-' + REPLICATE", sql);
        Assert.Contains("CONVERT(varchar(11), customer.CustomerId)", sql);
        Assert.Contains("JOIN @Inserted AS added ON added.CustomerId = customer.CustomerId", sql);
        Assert.Contains("parent.CustomerAccount = source.ParentAccount", sql);
        var insert = Regex.Match(sql, @"INSERT INTO dbo.tbl_Customers\s*\((?<columns>[^)]+)\)");
        Assert.True(insert.Success);
        foreach (var forbidden in new[] { "CustomerId", "CustomerCode", "CustomerPassword", "CustomerSourceId", "CustomerCareerId", "CustomerRegion", "CustomerCardId" })
            Assert.DoesNotContain(forbidden, insert.Groups["columns"].Value.Split(',').Select(x => x.Trim()));
        Assert.DoesNotContain("e10adc3949ba59abbe56e057f20f883e", sql);
        foreach (var required in new[] { "CustomerRepresentativeName", "CustomerContactPersonName", "CustomerCompanyEn", "CustomerContactPersonTitleEn" })
            Assert.Contains(required, insert.Groups["columns"].Value);
    }

    [Fact]
    public void EmployeeSeed_UsesExplicitManagerHash_AndNewAccountSecurityState()
    {
        var sql = Read("seed-rbac-test-employees.sql");
        Assert.Contains("EmployeeType = 6 AND Status = 1", sql);
        Assert.Contains("@SourcePassword", sql);
        Assert.Contains("DefaultPage,", sql);
        Assert.Contains("MustChangePassword,", sql);
        Assert.Contains("PasswordChangedAt,", sql);
        Assert.Contains("SessionVersion,", sql);
        Assert.Contains("'/contracts',", sql);
        Assert.Matches(@"'/contracts',\s*0,\s*SYSUTCDATETIME\(\),\s*1,", sql);
        Assert.Contains("SET MustChangePassword = 0,", sql);
        Assert.Contains("SessionVersion = employee.SessionVersion + 1", sql);
        Assert.Contains("employee.EmployeeAccount = role.EmployeeAccount", sql);
        Assert.Contains("employee.EmployeeCode = role.EmployeeCode", sql);
        Assert.Contains("employee.EmployeeType = role.EmployeeType", sql);
        Assert.Contains("WHERE employee.MustChangePassword = 1", sql);
        Assert.Contains("department.DepartmentCode = role.DepartmentCode", sql);
        Assert.DoesNotContain("RecoveryEmailVerifiedAt,", sql);
    }
}
