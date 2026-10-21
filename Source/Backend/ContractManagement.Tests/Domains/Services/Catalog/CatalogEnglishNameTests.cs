using System.ComponentModel.DataAnnotations;
using ContractManagement.API.Domains.DTOs.Requests.Catalog;
using ContractManagement.Domains.Services.Catalog;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Tests.Domains.Services.Catalog;

public sealed class CatalogEnglishNameTests
{
    [Fact]
    public async Task PendingCatalogNames_RemainNullWhenRead_AndAreNotReplacedByVietnameseNames()
    {
        await using var db = await SeedAsync();
        (await db.TblProducts.SingleAsync()).ProductNameEn = null;
        (await db.TblServices.SingleAsync()).ServiceNameEn = null;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var products = new ProductService(db);
        var services = new ServiceService(db);
        Assert.Null((await products.GetByIdAsync(1)).ProductNameEn);
        Assert.Null((await services.GetByIdAsync(1)).ServiceNameEn);
        Assert.Single((await products.GetListAsync(new() { Keyword = "Sản phẩm" })).Items);
        Assert.Single((await services.GetListAsync(new() { Keyword = "Dịch vụ" })).Items);
        Assert.Empty((await products.GetListAsync(new() { Keyword = "Missing translation" })).Items);
        Assert.Empty((await services.GetListAsync(new() { Keyword = "Missing translation" })).Items);
        await products.UpdateAsync(1, new() { ProductName = "Sản phẩm", ProductNameEn = "Product", UnitName = "Giấy phép", UnitNameEn = "License", CategoryId = 1 });
        await services.UpdateAsync(1, new() { ServiceName = "Dịch vụ", ServiceNameEn = "Service", UnitName = "Tháng", UnitNameEn = "Month", ServiceTypeId = 1 }, 9);
        Assert.Equal("Product", (await products.GetByIdAsync(1)).ProductNameEn);
        Assert.Equal("Service", (await services.GetByIdAsync(1)).ServiceNameEn);
    }

    public static IEnumerable<object[]> InvalidNames()
    {
        foreach (var name in new[] { "", "  ", new string('A', 501) })
        {
            yield return [new CreateProductRequest { ProductName = "Phần mềm", ProductNameEn = name, CategoryId = 1 }, "ProductNameEn"];
            yield return [new UpdateProductRequest { ProductName = "Phần mềm", ProductNameEn = name, CategoryId = 1 }, "ProductNameEn"];
            yield return [new CreateServiceRequest { ServiceName = "Triển khai", ServiceNameEn = name, ServiceTypeId = 1 }, "ServiceNameEn"];
            yield return [new UpdateServiceRequest { ServiceName = "Triển khai", ServiceNameEn = name, ServiceTypeId = 1 }, "ServiceNameEn"];
        }
    }

    [Theory, MemberData(nameof(InvalidNames))]
    public void Requests_RequireEnglishNamesAndLimitToSnapshotLength(object request, string field)
    {
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), errors, true));
        Assert.Contains(errors, error => error.MemberNames.Contains(field));
    }

    [Theory]
    [InlineData(false, "")]
    [InlineData(true, " ")]
    [InlineData(false, null)]
    [InlineData(true, null)]
    public async Task ProductService_RejectsMissingEnglishNameWithoutMvc(bool update, string? name)
    {
        await using var db = await SeedAsync();
        var service = new ProductService(db);
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            if (update) await service.UpdateAsync(1, new() { ProductName = "Đã sửa", ProductNameEn = name!, CategoryId = 1 });
            else await service.CreateAsync(new() { ProductName = "Mới", ProductNameEn = name!, CategoryId = 1 });
        });
        Assert.Equal("Original product", (await db.TblProducts.SingleAsync()).ProductNameEn);
    }

    [Theory]
    [InlineData(false, "")]
    [InlineData(true, " ")]
    [InlineData(false, null)]
    [InlineData(true, null)]
    public async Task ServiceService_RejectsMissingEnglishNameWithoutMvc(bool update, string? name)
    {
        await using var db = await SeedAsync();
        var service = new ServiceService(db);
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            if (update) await service.UpdateAsync(1, new() { ServiceName = "Đã sửa", ServiceNameEn = name!, ServiceTypeId = 1 }, 9);
            else await service.CreateAsync(new() { ServiceName = "Mới", ServiceNameEn = name!, ServiceTypeId = 1 }, 9);
        });
        Assert.Equal("Original service", (await db.TblServices.SingleAsync()).ServiceNameEn);
    }

    [Fact]
    public async Task CatalogEnglishNames_TrimPersistProjectAndSearchOnCreateAndUpdate()
    {
        await using var db = await SeedAsync();
        var products = new ProductService(db);
        var services = new ServiceService(db);
        var product = await products.CreateAsync(new() { ProductName = "Phần mềm", ProductNameEn = "  Software supply  ", UnitName = "Giấy phép", UnitNameEn = "License", CategoryId = 1 });
        var service = await services.CreateAsync(new() { ServiceName = "Triển khai", ServiceNameEn = "  Implementation  ", UnitName = "Tháng", UnitNameEn = "Month", ServiceTypeId = 1 }, 9);
        Assert.Equal("Software supply", product.ProductNameEn);
        Assert.Equal("Implementation", service.ServiceNameEn);
        await products.UpdateAsync(product.ProductId, new() { ProductName = "Phần mềm", ProductNameEn = "  Software package  ", UnitName = "Giấy phép", UnitNameEn = "License", CategoryId = 1 });
        await services.UpdateAsync(service.ServiceId, new() { ServiceName = "Triển khai", ServiceNameEn = "  Deployment service  ", UnitName = "Tháng", UnitNameEn = "Month", ServiceTypeId = 1 }, 9);
        Assert.Equal("Software package", (await products.GetByIdAsync(product.ProductId)).ProductNameEn);
        Assert.Equal("Deployment service", (await services.GetByIdAsync(service.ServiceId)).ServiceNameEn);
        Assert.Equal(product.ProductId, Assert.Single((await products.GetListAsync(new() { Keyword = "Software package" })).Items).ProductId);
        Assert.Equal(service.ServiceId, Assert.Single((await services.GetListAsync(new() { Keyword = "Deployment service" })).Items).ServiceId);
    }

    private static async Task<DbDtctechContext> SeedAsync()
    {
        var db = new DbDtctechContext(new DbContextOptionsBuilder<DbDtctechContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.TblCategories.Add(new() { CategoryId = 1, CategoryName = "Danh mục" });
        db.TblServiceTypes.Add(new() { ServiceTypeId = 1, ServiceTypeName = "Loại dịch vụ" });
        db.TblProducts.Add(new() { ProductId = 1, ProductName = "Sản phẩm", ProductNameEn = "Original product", CategoryId = 1, Status = 1 });
        db.TblServices.Add(new() { ServiceId = 1, ServiceName = "Dịch vụ", ServiceNameEn = "Original service", ServiceTypeId = 1, Status = 1 });
        await db.SaveChangesAsync();
        return db;
    }
}
