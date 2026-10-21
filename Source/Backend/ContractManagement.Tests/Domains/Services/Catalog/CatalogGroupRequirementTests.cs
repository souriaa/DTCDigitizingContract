using System.ComponentModel.DataAnnotations;
using ContractManagement.API.Common.Exceptions;
using ContractManagement.API.Domains.DTOs.Requests.Catalog;
using ContractManagement.Domains.Services.Catalog;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Tests.Domains.Services.Catalog;

public sealed class CatalogGroupRequirementTests
{
    public static IEnumerable<object[]> InvalidRequests()
    {
        yield return [new CreateProductRequest { ProductName = "Product" }, "CategoryId"];
        yield return [new UpdateProductRequest { ProductName = "Product" }, "CategoryId"];
        yield return [new CreateServiceRequest { ServiceName = "Service" }, "ServiceTypeId"];
        yield return [new UpdateServiceRequest { ServiceName = "Service" }, "ServiceTypeId"];
        yield return [new CreateProductRequest { ProductName = "Product", CategoryId = 0 }, "CategoryId"];
        yield return [new UpdateProductRequest { ProductName = "Product", CategoryId = 256 }, "CategoryId"];
        yield return [new CreateServiceRequest { ServiceName = "Service", ServiceTypeId = 0 }, "ServiceTypeId"];
        yield return [new UpdateServiceRequest { ServiceName = "Service", ServiceTypeId = 0 }, "ServiceTypeId"];
    }

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public void RequestValidation_RejectsMissingOrOutOfRangeGroup(object request, string field)
    {
        var results = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), results, true));
        Assert.Contains(results, result => result.MemberNames.Contains(field));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Product_RequiresGroupForCreateAndUpdate_EvenWhenCalledWithoutMvc(bool update)
    {
        await using var db = await SeedAsync();
        var service = new ProductService(db);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(async () =>
        {
            if (update) await service.UpdateAsync(1, new UpdateProductRequest { ProductName = "Changed" });
            else await service.CreateAsync(new CreateProductRequest { ProductName = "New" });
        });
        Assert.Equal(400, error.StatusCode);
        Assert.Equal("ProductCategoryRequired", error.Code);
        db.ChangeTracker.Clear();
        var product = Assert.Single(await db.TblProducts.ToListAsync());
        Assert.Equal("Original", product.ProductName);
        Assert.Equal(1, product.CategoryId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Service_RequiresTypeForCreateAndUpdate_EvenWhenCalledWithoutMvc(bool update)
    {
        await using var db = await SeedAsync();
        var service = new ServiceService(db);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(async () =>
        {
            if (update) await service.UpdateAsync(1, new UpdateServiceRequest { ServiceName = "Changed" }, 9);
            else await service.CreateAsync(new CreateServiceRequest { ServiceName = "New" }, 9);
        });
        Assert.Equal(400, error.StatusCode);
        Assert.Equal("ServiceTypeRequired", error.Code);
        db.ChangeTracker.Clear();
        var item = Assert.Single(await db.TblServices.ToListAsync());
        Assert.Equal("Original", item.ServiceName);
        Assert.Equal((byte)1, item.ServiceTypeId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NonexistentGroups_StillRejectBothCreateAndUpdate(bool update)
    {
        await using var db = await SeedAsync();
        var products = new ProductService(db);
        var services = new ServiceService(db);
        await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
        {
            if (update) await products.UpdateAsync(1, new UpdateProductRequest { ProductName = "Changed", CategoryId = 99 });
            else await products.CreateAsync(new CreateProductRequest { ProductName = "New", CategoryId = 99 });
        });
        await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
        {
            if (update) await services.UpdateAsync(1, new UpdateServiceRequest { ServiceName = "Changed", ServiceTypeId = 99 }, 9);
            else await services.CreateAsync(new CreateServiceRequest { ServiceName = "New", ServiceTypeId = 99 }, 9);
        });
        Assert.Single(db.TblProducts);
        Assert.Single(db.TblServices);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(256)]
    public async Task InvalidCategoryRange_AlsoRejectsDirectServiceCall(int categoryId)
    {
        await using var db = await SeedAsync();
        var service = new ProductService(db);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(
            new CreateProductRequest { ProductName = "New", CategoryId = categoryId }));
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync(1,
            new UpdateProductRequest { ProductName = "Changed", CategoryId = categoryId }));
    }

    [Fact]
    public async Task ZeroServiceType_AlsoRejectsDirectServiceCall()
    {
        await using var db = await SeedAsync();
        var service = new ServiceService(db);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(
            new CreateServiceRequest { ServiceName = "New", ServiceTypeId = 0 }, 9));
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync(1,
            new UpdateServiceRequest { ServiceName = "Changed", ServiceTypeId = 0 }, 9));
    }

    [Fact]
    public async Task ValidParentOrChildCategory_AndServiceType_WorkForBothCreateAndUpdate()
    {
        await using var db = await SeedAsync();
        db.TblCategories.Add(new TblCategory { CategoryId = 2, CategoryParentId = 1, CategoryName = "Child" });
        db.TblServiceTypes.Add(new TblServiceType { ServiceTypeId = 2, ServiceTypeName = "Second" });
        await db.SaveChangesAsync();
        var products = new ProductService(db);
        var created = await products.CreateAsync(new CreateProductRequest { ProductName = "New", ProductNameEn = "New product", UnitName = "Giấy phép", UnitNameEn = "License", CategoryId = 2 });
        Assert.Equal(2, created.CategoryId);
        await products.UpdateAsync(created.ProductId, new UpdateProductRequest { ProductName = "Updated", ProductNameEn = "Updated product", UnitName = "Giấy phép", UnitNameEn = "License", CategoryId = 1 });
        Assert.Equal(1, (await products.GetByIdAsync(created.ProductId)).CategoryId);
        var services = new ServiceService(db);
        var child = await services.CreateAsync(new CreateServiceRequest { ServiceName = "Child", ServiceNameEn = "Child service", UnitName = "Tháng", UnitNameEn = "Month", ServiceTypeId = 2, ServiceParentId = 1 }, 9);
        Assert.Equal((byte)2, child.ServiceTypeId);
        Assert.Equal(1, child.ServiceParentId);
        await services.UpdateAsync(child.ServiceId, new UpdateServiceRequest { ServiceName = "Updated", ServiceNameEn = "Updated service", UnitName = "Tháng", UnitNameEn = "Month", ServiceTypeId = 1 }, 9);
        var updated = await services.GetByIdAsync(child.ServiceId);
        Assert.Equal((byte)1, updated.ServiceTypeId);
        Assert.Null(updated.ServiceParentId);
    }

    private static async Task<DbDtctechContext> SeedAsync()
    {
        var db = new DbDtctechContext(new DbContextOptionsBuilder<DbDtctechContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.TblCategories.Add(new TblCategory { CategoryId = 1, CategoryName = "Parent" });
        db.TblServiceTypes.Add(new TblServiceType { ServiceTypeId = 1, ServiceTypeName = "First" });
        db.TblProducts.Add(new TblProduct { ProductId = 1, ProductName = "Original", CategoryId = 1, Status = 1 });
        db.TblServices.Add(new TblService { ServiceId = 1, ServiceName = "Original", ServiceTypeId = 1, Status = 1 });
        await db.SaveChangesAsync();
        return db;
    }
}
