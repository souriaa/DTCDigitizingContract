using ContractManagement.API.Common.Enums;
using ContractManagement.API.Common.Exceptions;
using ContractManagement.API.Domains.DTOs.Requests.Catalog;
using ContractManagement.Domains.Services.Catalog;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Tests.Domains.Services.Catalog;

public sealed class CatalogRestorationTests
{
    private static DbDtctechContext Context() => new(
        new DbContextOptionsBuilder<DbDtctechContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Restore_PreservesIdDataAndContractSnapshot_RemainsUnavailableUntilActivated(bool service)
    {
        await using var db = Context();
        db.TblProducts.Add(new TblProduct { ProductId = 1, ProductCode = "SP001", ProductName = "Sản phẩm", ProductPrice = 50, Status = 2 });
        db.TblServices.Add(new TblService { ServiceId = 1, ServiceName = "Dịch vụ", ServiceContent = "Nội dung gốc", ServicePrice = 50, Status = 2 });
        db.TblContractItems.Add(new TblContractItem
        {
            ContractItemId = 1, ContractId = 1, VersionId = 1,
            ItemType = service ? (byte)2 : (byte)1,
            SourceProductId = service ? null : 1, SourceServiceId = service ? 1 : null,
            ItemName = "Tên đã chốt", Quantity = 2, UnitPrice = 50, LineTotal = 100, RowVersion = [1]
        });
        await db.SaveChangesAsync();

        if (service)
        {
            var catalog = new ServiceService(db);
            await catalog.RestoreAsync(1, 9);
            var restored = await catalog.GetByIdAsync(1);
            Assert.Equal((byte)0, restored.Status);
            Assert.Equal("Nội dung gốc", restored.ServiceContent);
            Assert.Equal(50d, restored.ServicePrice);
            Assert.Equal(9, restored.UserModified);
            Assert.NotNull(restored.DateModified);
            Assert.Empty((await catalog.GetListAsync(new ServiceFilterRequest { Status = 1 })).Items);
            Assert.Empty((await catalog.GetListAsync(new ServiceFilterRequest { Status = 2 })).Items);
            Assert.Single((await catalog.GetListAsync(new ServiceFilterRequest())).Items);
            await catalog.SetStatusAsync(1, 1, 9);
            Assert.Single((await catalog.GetListAsync(new ServiceFilterRequest { Status = 1 })).Items);
        }
        else
        {
            var catalog = new ProductService(db);
            await catalog.RestoreAsync(1);
            var restored = await catalog.GetByIdAsync(1);
            Assert.Equal((byte)0, restored.Status);
            Assert.Equal("SP001", restored.ProductCode);
            Assert.Equal(50d, restored.ProductPrice);
            Assert.Empty((await catalog.GetListAsync(new ProductFilterRequest { Status = 1 })).Items);
            Assert.Empty((await catalog.GetListAsync(new ProductFilterRequest { Status = 2 })).Items);
            Assert.Single((await catalog.GetListAsync(new ProductFilterRequest())).Items);
            await catalog.SetStatusAsync(1, 1);
            Assert.Single((await catalog.GetListAsync(new ProductFilterRequest { Status = 1 })).Items);
        }
        var snapshot = await db.TblContractItems.SingleAsync();
        Assert.Equal("Tên đã chốt", snapshot.ItemName);
        Assert.Equal(100m, snapshot.LineTotal);
        Assert.Equal(service ? null : (int?)1, snapshot.SourceProductId);
        Assert.Equal(service ? (int?)1 : null, snapshot.SourceServiceId);
        Assert.Single(db.TblProducts);
        Assert.Single(db.TblServices);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Restore_RejectsNonDeletedItems(int status)
    {
        await using var db = Context();
        db.TblProducts.Add(new TblProduct { ProductId = 1, Status = (byte)status });
        db.TblServices.Add(new TblService { ServiceId = 1, Status = (byte)status });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ProductService(db).RestoreAsync(1));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ServiceService(db).RestoreAsync(1, 9));
        Assert.Equal((byte)status, (await db.TblProducts.SingleAsync()).Status);
        Assert.Equal((byte)status, (await db.TblServices.SingleAsync()).Status);
    }

    [Fact]
    public async Task Restore_RejectsMissingItems()
    {
        await using var db = Context();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => new ProductService(db).RestoreAsync(99));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => new ServiceService(db).RestoreAsync(99, 9));
    }

    [Fact]
    public async Task RestoreService_RequiresParentFirst_UpdatesHasChild_ProtectsRestoredChildFromParentDeletion()
    {
        await using var db = Context();
        db.TblServices.AddRange(
            new TblService { ServiceId = 1, ServiceName = "Cha", Status = 2, HasChild = false },
            new TblService { ServiceId = 2, ServiceName = "Con", Status = 2, ServiceParentId = 1 });
        await db.SaveChangesAsync();
        var catalog = new ServiceService(db);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => catalog.RestoreAsync(2, 9));
        Assert.Contains("khôi phục dịch vụ cha trước", error.Message);
        Assert.Equal((byte)2, (await db.TblServices.FindAsync(2))!.Status);
        await catalog.RestoreAsync(1, 9);
        await catalog.RestoreAsync(2, 9);
        Assert.True((await db.TblServices.FindAsync(1))!.HasChild);
        Assert.Equal(1, (await db.TblServices.FindAsync(2))!.ServiceParentId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => catalog.DeleteAsync(1, 9));
    }

    [Fact]
    public async Task DuplicateDeletedProductCode_HasActionableError_ForCreateAndUpdate()
    {
        await using var db = Context();
        db.TblCategories.Add(new TblCategory { CategoryId = 1, CategoryName = "Danh mục" });
        db.TblProducts.AddRange(
            new TblProduct { ProductId = 1, ProductCode = "SP001", ProductName = "Sản phẩm đã xóa", Status = 2 },
            new TblProduct { ProductId = 2, ProductCode = "SP002", ProductName = "Sản phẩm khác", Status = 1 });
        await db.SaveChangesAsync();
        var catalog = new ProductService(db);
        var createError = await Assert.ThrowsAsync<BusinessRuleException>(() => catalog.CreateAsync(
            new CreateProductRequest { CategoryId = 1, ProductCode = " SP001 ", ProductName = "Không được tạo trùng" }));
        Assert.Equal("ProductCodeDeleted", createError.Code);
        Assert.Equal(409, createError.StatusCode);
        Assert.Contains("Sản phẩm đã xóa", createError.Message);
        Assert.Contains("khôi phục", createError.Message);
        await Assert.ThrowsAsync<BusinessRuleException>(() => catalog.UpdateAsync(2,
            new UpdateProductRequest { CategoryId = 1, ProductCode = "SP001", ProductName = "Không được ghi đè" }));
        Assert.Equal(2, await db.TblProducts.CountAsync());
        Assert.Equal("SP002", (await db.TblProducts.FindAsync(2))!.ProductCode);
        await catalog.RestoreAsync(1);
        await Assert.ThrowsAsync<InvalidOperationException>(() => catalog.CreateAsync(
            new CreateProductRequest { CategoryId = 1, ProductCode = "SP001", ProductName = "Vẫn không được tạo trùng" }));
    }

    [Fact]
    public async Task DeletedList_IsExplicitAndSearchable_WithPagination()
    {
        await using var db = Context();
        for (var id = 1; id <= 3; id++)
        {
            db.TblProducts.Add(new TblProduct { ProductId = id, ProductCode = $"SP{id}", ProductName = "Archived", Status = 2 });
            db.TblServices.Add(new TblService { ServiceId = id, ServiceName = $"Archived {id}", Status = 2 });
        }
        db.TblProducts.Add(new TblProduct { ProductId = 4, ProductCode = "ACTIVE", Status = 1 });
        db.TblServices.Add(new TblService { ServiceId = 4, ServiceName = "Active", Status = 1 });
        await db.SaveChangesAsync();
        var products = new ProductService(db);
        var services = new ServiceService(db);
        Assert.Single((await products.GetListAsync(new ProductFilterRequest())).Items);
        Assert.Single((await services.GetListAsync(new ServiceFilterRequest())).Items);
        var productPage = await products.GetListAsync(new ProductFilterRequest { Status = 2, Keyword = "SP", Page = 2, PageSize = 1 });
        var servicePage = await services.GetListAsync(new ServiceFilterRequest { Status = 2, Keyword = "Archived", Page = 2, PageSize = 1 });
        Assert.Equal(3, productPage.TotalCount);
        Assert.Equal(3, servicePage.TotalCount);
        Assert.Equal(2, Assert.Single(productPage.Items).ProductId);
        Assert.Equal(2, Assert.Single(servicePage.Items).ServiceId);
        Assert.Single((await products.GetListAsync(new ProductFilterRequest { Status = 2, Keyword = "SP1" })).Items);
        Assert.Single((await services.GetListAsync(new ServiceFilterRequest { Status = 2, Keyword = "Archived 1" })).Items);
        Assert.Single(await services.GetOptionsAsync());
    }
}
