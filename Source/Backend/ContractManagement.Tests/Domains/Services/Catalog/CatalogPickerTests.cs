using ContractManagement.API.Common.Enums;
using ContractManagement.API.Domains.DTOs.Requests.Catalog;
using ContractManagement.Domains.Services.Catalog;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Tests.Domains.Services.Catalog;

public sealed class CatalogPickerTests
{
    private static async Task<DbDtctechContext> SeedAsync()
    {
        var db = new DbDtctechContext(new DbContextOptionsBuilder<DbDtctechContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        for (var i = 1; i <= 109; i++) db.TblProducts.Add(new() {
            ProductId = i, ProductCode = $"P-{i:000}", ProductName = $"Product {i}",
            ProductNameEn = i == 5 ? "English-only translation" : $"English product {i}",
            UnitName = "Giấy phép", UnitNameEn = "License", ProductPrice = i,
            Status = i == 108 ? (byte)0 : i == 109 ? (byte)2 : (byte)1 });
        for (var i = 1; i <= 11; i++) db.TblServices.Add(new() {
            ServiceId = i, ServiceName = $"Service {i}", ServiceNameEn = $"English service {i}",
            ServiceShortDesc = i == 4 ? "special consultation" : null,
            UnitName = "Gói", UnitNameEn = "Package", ServicePrice = i,
            Status = i == 10 ? (byte)0 : i == 11 ? (byte)2 : (byte)1 });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear(); return db;
    }

    [Fact]
    public async Task CombinedPagination_CrossesTypesAndIncludesRowsBeyondFirstHundredExactlyOnce()
    {
        await using var db = await SeedAsync(); var service = new CatalogWorkspaceService(db);
        var boundary = await service.GetPickerItemsAsync(new() { Page = 22, PageSize = 5 });
        Assert.Equal(116, boundary.TotalCount); Assert.Equal(24, boundary.TotalPages);
        Assert.Equal(["Product:2", "Product:1", "Service:9", "Service:8", "Service:7"],
            boundary.Items.Select(x => $"{x.ItemType}:{x.SourceId}").ToArray());
        var keys = new List<string>();
        for (var page = 1; page <= boundary.TotalPages; page++)
        {
            var result = await service.GetPickerItemsAsync(new() { Page = page, PageSize = 5 });
            Assert.InRange(result.Items.Count, 1, 5);
            keys.AddRange(result.Items.Select(x => $"{x.ItemType}:{x.SourceId}"));
        }
        Assert.Equal(116, keys.Count); Assert.Equal(116, keys.Distinct().Count());
        Assert.Contains("Product:1", keys); Assert.Contains("Service:1", keys);
        Assert.DoesNotContain("Product:108", keys); Assert.DoesNotContain("Product:109", keys);
        Assert.DoesNotContain("Service:10", keys); Assert.DoesNotContain("Service:11", keys);
    }

    [Fact]
    public async Task SelectedIds_AreOptionalAndCannotBypassStatusOrDuplicateResults()
    {
        await using var db = await SeedAsync(); var service = new CatalogWorkspaceService(db);
        Assert.Equal(116, (await service.GetPickerItemsAsync(new() { ProductIds = [2] })).TotalCount);
        Assert.Empty((await service.GetPickerItemsAsync(new() { SelectedOnly = true })).Items);
        var selected = new CatalogPickerFilter { SelectedOnly = true, ProductIds = [104, 2, 2, 108, 109], ServiceIds = [1, 3, 10, 11], PageSize = 2 };
        var first = await service.GetPickerItemsAsync(selected);
        Assert.Equal(4, first.TotalCount); Assert.Equal(2, first.TotalPages);
        Assert.Equal([104, 2], first.Items.Select(x => x.SourceId).ToArray());
        Assert.All(first.Items, x => Assert.Equal(ContractItemType.Product, x.ItemType));
        selected.Page = 2;
        var second = await service.GetPickerItemsAsync(selected);
        Assert.Equal([3, 1], second.Items.Select(x => x.SourceId).ToArray());
        selected.ItemType = ContractItemType.Service;
        Assert.Equal(2, (await service.GetPickerItemsAsync(selected)).TotalCount);
    }

    [Theory]
    [InlineData("P-005", ContractItemType.Product, 5)]
    [InlineData("English-only translation", ContractItemType.Product, 5)]
    [InlineData("special consultation", ContractItemType.Service, 4)]
    public async Task Search_UsesCodeEnglishNameAndDescriptionBeforeCountAndPagination(string keyword, ContractItemType type, int id)
    {
        await using var db = await SeedAsync();
        var filter = new CatalogPickerFilter { Keyword = keyword, PageSize = 1, SelectedOnly = true, ProductIds = [5, 104], ServiceIds = [4, 7] };
        var result = await new CatalogWorkspaceService(db).GetPickerItemsAsync(filter);
        Assert.Equal(1, result.TotalCount);
        var item = Assert.Single(result.Items);
        Assert.Equal(type, item.ItemType); Assert.Equal(id, item.SourceId);
        Assert.False(string.IsNullOrWhiteSpace(item.UnitName)); Assert.False(string.IsNullOrWhiteSpace(item.UnitNameEn));
    }

    [Theory]
    [InlineData(ContractItemType.Product, 107)]
    [InlineData(ContractItemType.Service, 9)]
    public async Task TypeFilter_ReturnsOnlyRequestedType(ContractItemType type, int count)
    {
        await using var db = await SeedAsync();
        var result = await new CatalogWorkspaceService(db).GetPickerItemsAsync(new() { ItemType = type });
        Assert.Equal(count, result.TotalCount); Assert.All(result.Items, x => Assert.Equal(type, x.ItemType));
    }

    [Theory]
    [InlineData(ContractItemType.Product)]
    [InlineData(ContractItemType.Service)]
    public async Task ErrorFocus_PinsSourceOnFirstPageWithoutDuplicatesOrChangingTotal(ContractItemType type)
    {
        await using var db = await SeedAsync(); var service = new CatalogWorkspaceService(db);
        var filter = new CatalogPickerFilter { FocusItemType = type, FocusItemId = 1, PageSize = 100 };
        var first = await service.GetPickerItemsAsync(filter);
        Assert.Equal(type, first.Items[0].ItemType); Assert.Equal(1, first.Items[0].SourceId);
        Assert.Equal(116, first.TotalCount); filter.Page = 2;
        var second = await service.GetPickerItemsAsync(filter);
        Assert.Equal(116, first.Items.Concat(second.Items).Select(x => $"{x.ItemType}:{x.SourceId}").Distinct().Count());
        filter.Page = 1; filter.SelectedOnly = true; filter.ProductIds = [2]; filter.ServiceIds = [];
        Assert.Equal(2, Assert.Single((await service.GetPickerItemsAsync(filter)).Items).SourceId);
    }

    [Fact]
    public async Task OldProductAndServiceApis_KeepExistingDefaultVisibilityAndPaging()
    {
        await using var db = await SeedAsync();
        var products = await new ProductService(db).GetListAsync(new());
        var services = await new ServiceService(db).GetListAsync(new());
        Assert.Equal(108, products.TotalCount); Assert.Equal(20, products.PageSize);
        Assert.Equal(10, services.TotalCount); Assert.Equal(20, services.PageSize);
        Assert.Contains(products.Items, x => x.Status == 0); Assert.Contains(services.Items, x => x.Status == 0);
    }

    [Fact]
    public async Task InvalidFilters_AreRejectedBeforeQuery()
    {
        await using var db = await SeedAsync(); var service = new CatalogWorkspaceService(db);
        CatalogPickerFilter[] invalid = [new() { Page = 0 }, new() { PageSize = 101 },
            new() { Page = int.MaxValue, PageSize = 100 }, new() { Keyword = new string('x', 501) },
            new() { ItemType = (ContractItemType)3 }, new() { ProductIds = [0] }, new() { ServiceIds = [-1] },
            new() { FocusItemId = 1 }, new() { FocusItemType = ContractItemType.Product }];
        foreach (var filter in invalid) await Assert.ThrowsAsync<ArgumentException>(() => service.GetPickerItemsAsync(filter));
    }
}
