using ContractManagement.API.Domains.DTOs.Requests.Customer;
using ContractManagement.API.Domains.Services.Customer;
using ContractManagement.Infrastructure.Persistence.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ContractManagement.Tests.Domains.Services;

public sealed class CustomerEnglishFieldsTests
{
    [Fact]
    public async Task Customer_EnglishFieldsAreOptional_AndRoundTripWhenProvided()
    {
        await using var db = new DbDtctechContext(new DbContextOptionsBuilder<DbDtctechContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        var service = new CustomerService(db);
        var created = await service.CreateAsync(new CreateCustomerRequest { CustomerFullName = "Khách hàng" }, 1);
        Assert.Null(created.CustomerCompanyEn);
        Assert.Null(created.CustomerContactPersonTitleEn);
        await service.UpdateAsync(created.CustomerId, new UpdateCustomerRequest
        {
            CustomerFullName = "Khách hàng", CustomerCompanyEn = " Sample Customer ",
            CustomerAddressEn = " Hanoi ", CustomerBankNameEn = " Sample Bank ",
            CustomerContactPersonTitleEn = " Project Manager ",
            CustomerRepresentativeTitleEn = " Director ", CustomerCityEn = "   "
        }, 1);
        var result = await service.GetByIdAsync(created.CustomerId);
        Assert.Equal("Sample Customer", result.CustomerCompanyEn);
        Assert.Equal("Hanoi", result.CustomerAddressEn);
        Assert.Equal("Sample Bank", result.CustomerBankNameEn);
        Assert.Equal("Project Manager", result.CustomerContactPersonTitleEn);
        Assert.Equal("Director", result.CustomerRepresentativeTitleEn);
        Assert.Null(result.CustomerCityEn);
    }
}
