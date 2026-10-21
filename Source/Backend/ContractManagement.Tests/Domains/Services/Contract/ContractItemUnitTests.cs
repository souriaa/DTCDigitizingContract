using ContractManagement.API.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Tests.Domains.Services.Contract;

public sealed partial class ContractServiceSlice04Tests
{
    [Theory]
    [InlineData(ContractItemType.Product, false)]
    [InlineData(ContractItemType.Service, false)]
    [InlineData(ContractItemType.Product, true)]
    [InlineData(ContractItemType.Service, true)]
    public async Task Create_MissingCatalogUnitCannotBeReplacedByClientInput(ContractItemType type, bool english)
    {
        await using var db = CreateContext();
        await SeedCreateDependenciesAsync(db);
        (await db.TblContractTemplates.SingleAsync()).LanguageMode = (byte)ContractLanguageMode.Bilingual;
        if (type == ContractItemType.Product)
        {
            var product = await db.TblProducts.SingleAsync();
            if (english) product.UnitNameEn = null; else product.UnitName = null;
        }
        else
        {
            var service = await db.TblServices.SingleAsync();
            if (english) service.UnitNameEn = null; else service.UnitName = null;
        }
        await db.SaveChangesAsync();
        var request = CreateRequest("VND", [CreateItem(type, 1, 100,
            sourceProductId: type == ContractItemType.Product ? ProductId : null,
            sourceServiceId: type == ContractItemType.Service ? ServiceId : null)]);
        request.LanguageMode = ContractLanguageMode.Bilingual;
        request.ContractNameEn = "Contract";
        request.Items[0].UnitName = "Forged"; request.Items[0].UnitNameEn = "Forged";
        var error = await Assert.ThrowsAsync<ArgumentException>(() => CreateService(db).CreateAsync(request, EmployeeId));
        Assert.Contains("Đơn vị", error.Message);
        Assert.Empty(db.TblContracts);
    }

    [Theory]
    [InlineData(ContractItemType.Product, ContractLanguageMode.Vietnamese)]
    [InlineData(ContractItemType.Service, ContractLanguageMode.Vietnamese)]
    [InlineData(ContractItemType.Product, ContractLanguageMode.Bilingual)]
    [InlineData(ContractItemType.Service, ContractLanguageMode.Bilingual)]
    public async Task Create_ItemUnitsFromCatalog_IgnoreClientInputAndRemainSnapshots(ContractItemType type, ContractLanguageMode language)
    {
        await using var db = CreateContext();
        await SeedCreateDependenciesAsync(db);
        (await db.TblContractTemplates.SingleAsync()).LanguageMode = (byte)language;
        await db.SaveChangesAsync();
        var request = CreateRequest("VND", [CreateItem(type, 2, 100,
            sourceProductId: type == ContractItemType.Product ? ProductId : null,
            sourceServiceId: type == ContractItemType.Service ? ServiceId : null)]);
        request.LanguageMode = language;
        request.ContractNameEn = "Software agreement";
        request.Items[0].UnitName = "Forged unit";
        request.Items[0].UnitNameEn = "Forged English unit";
        var service = CreateService(db);
        var created = await service.CreateAsync(request, EmployeeId);
        db.ChangeTracker.Clear();
        var detail = await service.GetDetailAsync(created.ContractId, EmployeeId);
        var item = Assert.Single(detail.CurrentVersion.Items);
        Assert.Equal("Giấy phép", item.UnitName);
        Assert.Equal("License", item.UnitNameEn);
        Assert.DoesNotContain(detail.ApprovalReadiness.Blockers, x => x.Code is "ContractItemUnitRequired" or "BilingualItemUnitRequired");
        var product = await db.TblProducts.SingleAsync(); product.UnitName = "Tháng"; product.UnitNameEn = "Month";
        var masterService = await db.TblServices.SingleAsync(); masterService.UnitName = "Tháng"; masterService.UnitNameEn = "Month";
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var saved = Assert.Single((await service.GetDetailAsync(created.ContractId, EmployeeId)).CurrentVersion.Items);
        Assert.Equal("Giấy phép", saved.UnitName); Assert.Equal("License", saved.UnitNameEn);
    }
}
