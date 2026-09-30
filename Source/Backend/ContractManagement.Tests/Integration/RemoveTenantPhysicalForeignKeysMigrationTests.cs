using ContractManagement.Infrastructure.Migrations.Application;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace ContractManagement.Tests.Integration;

public sealed class RemoveTenantPhysicalForeignKeysMigrationTests
{
    [Fact]
    public void Up_DropsOnlyTheReviewed58ForeignKeys()
    {
        var operations = new RemoveTenantPhysicalForeignKeys().UpOperations;
        var drops = operations.OfType<DropForeignKeyOperation>().ToArray();

        Assert.Equal(58, drops.Length);
        Assert.Equal(58, drops.Select(operation => operation.Name).Distinct().Count());
        Assert.All(drops, operation =>
        {
            Assert.StartsWith("FK_tbl_", operation.Name);
            Assert.StartsWith("tbl_", operation.Table);
        });
        Assert.All(operations, operation =>
            Assert.True(operation is DropForeignKeyOperation or SqlOperation,
                $"Unexpected schema operation: {operation.GetType().Name}"));
    }

    [Fact]
    public void Down_RestoresExactlyTheReviewedForeignKeys()
    {
        var migration = new RemoveTenantPhysicalForeignKeys();
        var dropped = migration.UpOperations.OfType<DropForeignKeyOperation>()
            .Select(operation => operation.Name).Order().ToArray();
        var restored = migration.DownOperations.OfType<AddForeignKeyOperation>()
            .ToArray();

        Assert.Equal(dropped, restored.Select(operation => operation.Name).Order());
        Assert.Equal(16, restored.Count(operation =>
            operation.OnDelete == Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.Cascade));
        Assert.All(migration.DownOperations, operation =>
            Assert.IsType<AddForeignKeyOperation>(operation));
    }
}
