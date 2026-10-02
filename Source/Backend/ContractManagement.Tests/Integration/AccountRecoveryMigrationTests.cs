using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Central;
using ContractManagement.Infrastructure.Persistence.Recovery;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace ContractManagement.Tests.Integration;

public sealed class AccountRecoveryMigrationTests
{
    [Theory, InlineData(true), InlineData(false)]
    public void Schema_OnlyAddsRecoveryFieldsTablesAndIndexes_NoForeignKeys(bool central)
    {
        Microsoft.EntityFrameworkCore.Migrations.Migration migration = central
            ? new ContractManagement.Infrastructure.Migrations.Central.AccountRecoveryPhase123()
            : new ContractManagement.Infrastructure.Migrations.Tenant.AccountRecoveryPhase123();
        var column = Assert.Single(migration.UpOperations.OfType<AddColumnOperation>());
        Assert.Equal("RecoveryEmailVerifiedAt", column.Name); Assert.True(column.IsNullable);
        Assert.Equal(2, migration.UpOperations.OfType<CreateTableOperation>().Count());
        Assert.All(migration.UpOperations.OfType<CreateTableOperation>(), table => Assert.Empty(table.ForeignKeys));
        Assert.All(migration.UpOperations, operation => Assert.True(operation is AddColumnOperation or CreateTableOperation or CreateIndexOperation));
        using DbContext db = central ? new CentralDbContext(new DbContextOptionsBuilder<CentralDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True").Options)
            : new DbDtctechContext(new DbContextOptionsBuilder<DbDtctechContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True").Options);
        foreach (var type in new[] { typeof(AccountRecoveryChallenge), typeof(AccountRecoveryEmailOutbox) })
        {
            var entity = db.Model.FindEntityType(type)!;
            Assert.Empty(entity.GetForeignKeys());
            Assert.True(entity.FindProperty("RowVersion")!.IsConcurrencyToken);
        }
        Assert.Equal("binary(32)", db.Model.FindEntityType(typeof(AccountRecoveryChallenge))!.FindProperty("TokenHash")!.GetColumnType());
    }
}
