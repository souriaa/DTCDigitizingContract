using ContractManagement.API.Common.Enums;
using ContractManagement.API.Domains.DTOs.Requests.Catalog;
using ContractManagement.Domains.Services.Catalog;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Tests.Domains.Services.Catalog;

public sealed class CatalogDeletionTests
{
    private static DbDtctechContext Context() => new(
        new DbContextOptionsBuilder<DbDtctechContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_HidesFromCatalogButPreservesContractSnapshotAndReferences(bool service)
    {
        await using var db = Context();
        db.TblProducts.Add(new TblProduct { ProductId = 1, ProductName = "Sản phẩm", Status = 1 });
        db.TblServices.Add(new TblService { ServiceId = 1, ServiceName = "Dịch vụ", Status = 1 });
        db.TblContractItems.Add(new TblContractItem
        {
            ContractItemId = 1, ContractId = 1, VersionId = 1,
            ItemType = service ? (byte)2 : (byte)1,
            SourceProductId = service ? null : 1,
            SourceServiceId = service ? 1 : null,
            ItemName = "Tên lưu trong hợp đồng", Quantity = 2, UnitPrice = 50,
            LineTotal = 100, RowVersion = [1],
        });
        db.TblQuotationDetails.Add(new TblQuotationDetail { QuotationDetailId = 1, ProductId = 1 });
        await db.SaveChangesAsync();

        if (service)
        {
            var catalog = new ServiceService(db);
            await catalog.DeleteAsync(1, 9);
            Assert.Equal(CatalogItemStatuses.Deleted, (await db.TblServices.SingleAsync()).Status);
            Assert.Empty((await catalog.GetListAsync(new ServiceFilterRequest())).Items);
            Assert.Single((await catalog.GetListAsync(new ServiceFilterRequest { Status = 2 })).Items);
            await Assert.ThrowsAsync<KeyNotFoundException>(() => catalog.GetByIdAsync(1));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => catalog.SetStatusAsync(1, 1, 9));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => catalog.UpdateAsync(1, new UpdateServiceRequest { ServiceName = "Mở lại" }, 9));
        }
        else
        {
            var catalog = new ProductService(db);
            await catalog.DeleteAsync(1);
            Assert.Equal(CatalogItemStatuses.Deleted, (await db.TblProducts.SingleAsync()).Status);
            Assert.Empty((await catalog.GetListAsync(new ProductFilterRequest())).Items);
            Assert.Single((await catalog.GetListAsync(new ProductFilterRequest { Status = 2 })).Items);
            await Assert.ThrowsAsync<KeyNotFoundException>(() => catalog.GetByIdAsync(1));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => catalog.SetStatusAsync(1, 1));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => catalog.UpdateAsync(1, new UpdateProductRequest { ProductName = "Mở lại" }));
        }

        db.ChangeTracker.Clear();
        var snapshot = await db.TblContractItems.SingleAsync();
        Assert.Equal("Tên lưu trong hợp đồng", snapshot.ItemName);
        Assert.Equal(100m, snapshot.LineTotal);
        Assert.Equal(service ? null : (int?)1, snapshot.SourceProductId);
        Assert.Equal(service ? (int?)1 : null, snapshot.SourceServiceId);
        Assert.Equal(1, (await db.TblQuotationDetails.SingleAsync()).ProductId);
    }

    [Fact]
    public async Task LockedItems_RemainInCatalogButNotInActivePicker_AndCanBeReopened()
    {
        await using var db = Context();
        db.TblProducts.AddRange(
            new TblProduct { ProductId = 1, ProductName = "Khóa", Status = 0 },
            new TblProduct { ProductId = 2, ProductName = "Hoạt động", Status = 1 });
        db.TblServices.AddRange(
            new TblService { ServiceId = 1, ServiceName = "Khóa", Status = 0 },
            new TblService { ServiceId = 2, ServiceName = "Hoạt động", Status = 1 });
        await db.SaveChangesAsync();
        var products = new ProductService(db);
        var services = new ServiceService(db);
        Assert.Equal(2, (await products.GetListAsync(new ProductFilterRequest())).TotalCount);
        Assert.Equal(2, (await services.GetListAsync(new ServiceFilterRequest())).TotalCount);
        Assert.Equal(2, Assert.Single((await products.GetListAsync(new ProductFilterRequest { Status = 1 })).Items).ProductId);
        Assert.Equal(2, Assert.Single((await services.GetListAsync(new ServiceFilterRequest { Status = 1 })).Items).ServiceId);
        await products.SetStatusAsync(1, 1);
        await services.SetStatusAsync(1, 1, 9);
        Assert.Equal(2, (await products.GetListAsync(new ProductFilterRequest { Status = 1 })).TotalCount);
        Assert.Equal(2, (await services.GetListAsync(new ServiceFilterRequest { Status = 1 })).TotalCount);
    }

    [Fact]
    public async Task ServiceParent_CannotBeDeletedBeforeChild_AndHasChildUpdatesAfterDeletion()
    {
        await using var db = Context();
        db.TblServices.AddRange(
            new TblService { ServiceId = 1, ServiceName = "Cha", HasChild = true, Status = 1 },
            new TblService { ServiceId = 2, ServiceName = "Con", ServiceParentId = 1, Status = 0 });
        await db.SaveChangesAsync();
        var catalog = new ServiceService(db);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => catalog.DeleteAsync(1, 9));
        Assert.Contains("dịch vụ con", error.Message);
        Assert.Equal((byte)1, (await db.TblServices.FindAsync(1))!.Status);
        await catalog.DeleteAsync(2, 9);
        Assert.False((await db.TblServices.FindAsync(1))!.HasChild);
        await catalog.DeleteAsync(1, 9);
        Assert.Empty((await catalog.GetListAsync(new ServiceFilterRequest())).Items);
    }

    [Fact]
    public async Task DeletedService_CannotBeSelectedAsParent()
    {
        await using var db = Context();
        db.TblServiceTypes.Add(new TblServiceType { ServiceTypeId = 1, ServiceTypeName = "Loại dịch vụ" });
        db.TblServices.Add(new TblService { ServiceId = 1, ServiceName = "Đã xóa", Status = 2 });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => new ServiceService(db).CreateAsync(
            new CreateServiceRequest { ServiceName = "Con", ServiceTypeId = 1, ServiceParentId = 1 }, 9));
        Assert.Single(db.TblServices);
    }
}
