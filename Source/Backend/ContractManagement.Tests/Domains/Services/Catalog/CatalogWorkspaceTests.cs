using ContractManagement.API.Common.Exceptions;
using ContractManagement.API.Common.Security;
using ContractManagement.API.Domains.Controllers.Catalog;
using ContractManagement.API.Domains.DTOs.Requests.Catalog;
using ContractManagement.API.Domains.Services.Catalog;
using ContractManagement.Domains.Services.Catalog;
using ContractManagement.Filter;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Tests.Domains.Services.Catalog;

public sealed class CatalogWorkspaceTests
{
    private static readonly DateTime Day = new(2026, 10, 14);
    private static async Task<DbDtctechContext> Seed()
    {
        var db = new DbDtctechContext(new DbContextOptionsBuilder<DbDtctechContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.TblCategories.AddRange(new TblCategory { CategoryId = 1, CategoryName = "Root" },
            new TblCategory { CategoryId = 2, CategoryName = "Child", CategoryParentId = 1 },
            new TblCategory { CategoryId = 3, CategoryName = "Leaf", CategoryParentId = 2 },
            new TblCategory { CategoryId = 4, CategoryName = "Empty", CategoryParentId = 1 });
        db.TblServiceTypes.AddRange(new TblServiceType { ServiceTypeId = 1, ServiceTypeName = "First" }, new TblServiceType { ServiceTypeId = 2, ServiceTypeName = "Second" });
        db.TblProducts.AddRange(
            new TblProduct { ProductId = 1, ProductCode = "ROOT", ProductName = "Root item", CategoryId = 1, Status = 1, ProductCreatedDate = Day },
            new TblProduct { ProductId = 2, ProductCode = "LEAF", ProductName = "Leaf item", CategoryId = 3, Status = 0, ProductCreatedDate = Day.AddHours(23), ProductDetails = "Must not be loaded by list" },
            new TblProduct { ProductId = 3, ProductName = "Deleted", CategoryId = 3, Status = 2, ProductCreatedDate = Day },
            new TblProduct { ProductId = 4, ProductName = "Unassigned", Status = 1, ProductCreatedDate = Day.AddDays(-1) });
        db.TblServices.AddRange(
            new TblService { ServiceId = 1, ServiceName = "Parent", ServiceTypeId = 1, Status = 1, DateCreated = Day },
            new TblService { ServiceId = 2, ServiceName = "Child", ServiceTypeId = 2, ServiceParentId = 1, Status = 0, DateCreated = Day.AddHours(23), ServiceContent = "Long body" },
            new TblService { ServiceId = 3, ServiceName = "Unassigned", ServiceParentId = 1, Status = 1, DateCreated = Day },
            new TblService { ServiceId = 4, ServiceName = "Deleted", ServiceTypeId = 2, Status = 2, DateCreated = Day });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return db;
    }

    [Fact]
    public async Task Tree_CountsDirectAndThreeLevelSubtrees_WithoutDuplicatingItems()
    {
        await using var db = await Seed(); var service = new CatalogWorkspaceService(db);
        var tree = await service.GetCategoriesAsync(new());
        Assert.Equal(3, tree.TotalCount); Assert.Equal(1, tree.UnassignedCount);
        var root = tree.Groups.Single(x => x.Id == 1);
        Assert.Equal(1, root.DirectCount); Assert.Equal(2, root.SubtreeCount); Assert.Equal(3, root.DescendantCount);
        Assert.Equal("Root / Child / Leaf", tree.Groups.Single(x => x.Id == 3).Path);
        Assert.Equal(0, tree.Groups.Single(x => x.Id == 2).DirectCount);
        Assert.Equal(1, tree.Groups.Single(x => x.Id == 2).SubtreeCount);
        Assert.Equal(1, Assert.Single((await service.GetProductsAsync(new() { GroupId = 1 })).Items).Id);
        Assert.Equal(2, Assert.Single((await service.GetProductsAsync(new() { GroupId = 3 })).Items).Id);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData(0, 1)] [InlineData(1, 2)] [InlineData(2, 1)]
    public async Task CountsAndListsShareStatusScope(byte status, int expected)
    {
        await using var db = await Seed(); var service = new CatalogWorkspaceService(db);
        var filter = new CatalogWorkspaceFilter { Status = status };
        Assert.Equal(expected, (await service.GetCategoriesAsync(filter)).TotalCount);
        Assert.Equal(expected, (await service.GetProductsAsync(filter)).TotalCount);
        Assert.Equal(expected, (await service.GetServiceTypesAsync(filter)).TotalCount);
        Assert.Equal(expected, (await service.GetServicesAsync(filter)).TotalCount);
    }

    [Fact]
    public async Task SearchAndDateRangeFindUnopenedDeepItemsAndEntireLastDay()
    {
        await using var db = await Seed(); var service = new CatalogWorkspaceService(db);
        var row = Assert.Single((await service.GetProductsAsync(new() { Keyword = "LEAF", FromDate = Day, ToDate = Day })).Items);
        Assert.Equal("Root / Child / Leaf", row.GroupPath);
        Assert.Equal(2, (await service.GetCategoriesAsync(new() { FromDate = Day, ToDate = Day })).TotalCount);
        Assert.Equal(2, (await service.GetProductsAsync(new() { FromDate = Day, ToDate = Day })).TotalCount);
        Assert.DoesNotContain(typeof(ContractManagement.API.Domains.DTOs.Responses.Catalog.CatalogItemSummaryResponse).GetProperties(), x => x.Name is "ProductDetails" or "ServiceContent");
    }

    [Fact]
    public async Task UnassignedFiltersDoNotReturnWholeCatalog_AndCrossTypeParentNameIsVisible()
    {
        await using var db = await Seed(); var service = new CatalogWorkspaceService(db);
        Assert.Equal(4, Assert.Single((await service.GetProductsAsync(new() { UnassignedOnly = true })).Items).Id);
        var unassigned = Assert.Single((await service.GetServicesAsync(new() { UnassignedOnly = true })).Items);
        Assert.Equal(3, unassigned.Id); Assert.Equal("Parent", unassigned.ParentName);
        var child = Assert.Single((await service.GetServicesAsync(new() { GroupId = 2 })).Items);
        Assert.Equal(2, child.Id); Assert.Equal("Second", child.GroupPath); Assert.Equal("Parent", child.ParentName);
    }

    [Fact]
    public async Task PaginationHasStableOrderAndNeverIncludesDeletedByDefault()
    {
        await using var db = await Seed(); var service = new CatalogWorkspaceService(db);
        Assert.Equal(4, Assert.Single((await service.GetProductsAsync(new() { PageSize = 1 })).Items).Id);
        Assert.Equal(2, Assert.Single((await service.GetProductsAsync(new() { PageSize = 1, Page = 2 })).Items).Id);
        Assert.Empty((await service.GetProductsAsync(new() { PageSize = 1, Page = 4 })).Items);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task InvalidHierarchyRaisesActionableErrorWithoutReassigningNodes(bool missingParent)
    {
        await using var db = await Seed();
        (await db.TblCategories.FindAsync((byte)1))!.CategoryParentId = missingParent ? (byte)99 : (byte)3;
        await db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => new CatalogWorkspaceService(db).GetCategoriesAsync(new()));
        Assert.Equal("CategoryHierarchyInvalid", error.Code);
        Assert.Equal(missingParent ? (byte)99 : (byte)3, (await db.TblCategories.FindAsync((byte)1))!.CategoryParentId);
    }

    [Fact]
    public async Task CategoryUpdateRejectsDescendantAsParent_ButAllowsValidReparenting()
    {
        await using var db = await Seed(); var service = new CategoryService(db);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.UpdateAsync(1, new() { CategoryName = "Root", CategoryParentId = 3 }));
        db.ChangeTracker.Clear(); Assert.Null((await db.TblCategories.FindAsync((byte)1))!.CategoryParentId);
        await service.UpdateAsync(3, new() { CategoryName = "Leaf", CategoryParentId = 1 });
        Assert.Equal((byte)1, (await db.TblCategories.FindAsync((byte)3))!.CategoryParentId);
    }

    [Fact]
    public async Task ArchivedDetailsAvailableOnlyAtExplicitReadBoundary_OldReadsStillReject()
    {
        await using var db = await Seed(); var products = new ProductService(db); var services = new ServiceService(db);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => products.GetByIdAsync(3));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => services.GetByIdAsync(4));
        Assert.Equal(3, (await products.GetByIdAsync(3, true)).ProductId);
        Assert.Equal(4, (await services.GetByIdAsync(4, true)).ServiceId);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => products.UpdateAsync(3, new() { ProductName = "Changed", CategoryId = 1 }));
    }

    [Fact]
    public async Task SeparateTenantContextsCannotSeeEachOthersGroupsOrItems()
    {
        await using var first = await Seed();
        await using var second = new DbDtctechContext(new DbContextOptionsBuilder<DbDtctechContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var service = new CatalogWorkspaceService(second);
        Assert.Empty((await service.GetCategoriesAsync(new())).Groups);
        Assert.Empty((await service.GetProductsAsync(new())).Items);
        Assert.Empty((await service.GetServicesAsync(new())).Items);
    }

    [Fact]
    public void ReadEndpointsRequireCatalogPermissionAndContradictoryFiltersAreRejected()
    {
        var attribute = Assert.Single(typeof(CatalogWorkspaceController).GetCustomAttributes(typeof(SessionAuthorizeAttribute), true).Cast<SessionAuthorizeAttribute>());
        Assert.NotNull(attribute);
        Assert.Throws<ArgumentException>(() => new CatalogWorkspaceFilter { GroupId = 1, UnassignedOnly = true }.EnsureValid());
        Assert.Throws<ArgumentException>(() => new CatalogWorkspaceFilter { FromDate = Day, ToDate = Day.AddDays(-1) }.EnsureValid());
        Assert.Throws<ArgumentException>(() => new CatalogWorkspaceFilter { PageSize = 101 }.EnsureValid());
        Assert.Throws<ArgumentException>(() => new CatalogWorkspaceFilter { Page = int.MaxValue }.EnsureValid());
    }
}
