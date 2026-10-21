using ContractManagement.API.Common.Enums;
using ContractManagement.API.Domains.DTOs.Requests.Dashboard;
using ContractManagement.API.Domains.Services.Dashboard;

namespace ContractManagement.Tests.Domains.Dashboard;

public sealed partial class DashboardPhase02Tests
{
    [Fact]
    public async Task CurrencyTotals_KeepPurchaseAndSaleSeparate_NotNetRevenue()
    {
        await using var db = CreateTenantContext();
        var now = DateTime.UtcNow;
        db.TblEmployees.Add(Employee(1, EmployeeType.Sale));
        var sale = Contract(101, 1, ContractStatus.Signed, 100m, "VND", now);
        var purchase = Contract(102, 1, ContractStatus.Signed, 60m, "VND", now);
        sale.ContractDirection = (byte)ContractDirection.Sale;
        purchase.ContractDirection = (byte)ContractDirection.Purchase;
        db.TblContracts.AddRange(sale, purchase);
        await db.SaveChangesAsync();
        var result = await new DashboardService(db).GetAsync(1, new DashboardFilterRequest { From = now.AddDays(-1), To = now.AddDays(1) });
        Assert.Equal(2, result.AmountByCurrency.Count);
        Assert.Equal(100m, result.AmountByCurrency.Single(x => x.ContractDirection == ContractDirection.Sale).Amount);
        Assert.Equal(60m, result.AmountByCurrency.Single(x => x.ContractDirection == ContractDirection.Purchase).Amount);
    }
}
