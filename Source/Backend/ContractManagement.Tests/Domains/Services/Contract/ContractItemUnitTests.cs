using ContractManagement.API.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Tests.Domains.Services.Contract;

public sealed partial class ContractServiceSlice04Tests
{
    [Theory]
    [InlineData(ContractItemType.Product, ContractLanguageMode.Vietnamese)]
    [InlineData(ContractItemType.Service, ContractLanguageMode.Vietnamese)]
    [InlineData(ContractItemType.Product, ContractLanguageMode.Bilingual)]
    [InlineData(ContractItemType.Service, ContractLanguageMode.Bilingual)]
    public async Task Create_ItemUnitsFromRequest_AreStoredAndReturned(ContractItemType type, ContractLanguageMode language)
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
        request.Items[0].UnitName = " Giấy phép ";
        request.Items[0].UnitNameEn = language == ContractLanguageMode.Bilingual ? " License " : null;
        var service = CreateService(db);
        var created = await service.CreateAsync(request, EmployeeId);
        db.ChangeTracker.Clear();
        var detail = await service.GetDetailAsync(created.ContractId, EmployeeId);
        var item = Assert.Single(detail.CurrentVersion.Items);
        Assert.Equal("Giấy phép", item.UnitName);
        Assert.Equal(language == ContractLanguageMode.Bilingual ? "License" : null, item.UnitNameEn);
        Assert.DoesNotContain(detail.ApprovalReadiness.Blockers, x => x.Code is "ContractItemUnitRequired" or "BilingualItemUnitRequired");
    }
}
