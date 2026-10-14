using System.Text.Json;
using ContractManagement.API.Common.Exceptions;
using ContractManagement.API.Domains.DTOs.Requests.Customer;
using ContractManagement.API.Domains.Services.Customer;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ContractManagement.Tests.Domains.Services.Customer;

public sealed class CustomerCodeServiceTests
{
    [Fact]
    public async Task Create_GeneratesAndReturnsPersistedCodeWithoutAcceptingClientCode()
    {
        await using var db = CreateContext();
        var service = new CustomerService(db);
        var request = JsonSerializer.Deserialize<CreateCustomerRequest>(
            """{"customerCode":"CLIENT-CODE","customerFullName":"  Khách hàng mới  "}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var created = await service.CreateAsync(request, 11);
        Assert.Equal($"CUST-{created.CustomerId:D6}", created.CustomerCode);
        Assert.Equal("Khách hàng mới", created.CustomerFullName);
        Assert.Equal((byte)1, created.Status);
        db.ChangeTracker.Clear();
        var persisted = await db.TblCustomers.SingleAsync();
        Assert.Equal(created.CustomerCode, persisted.CustomerCode);
        Assert.Equal(11, persisted.UserCreated);
        var search = await service.GetListAsync(new CustomerFilterRequest { Keyword = created.CustomerCode });
        Assert.Equal(created.CustomerId, Assert.Single(search.Items).CustomerId);
    }

    [Fact]
    public async Task ConcurrentCreates_UseDistinctDatabaseIdentitiesAndCodes()
    {
        var database = Guid.NewGuid().ToString();
        await using var firstDb = CreateContext(database);
        await using var secondDb = CreateContext(database);
        var results = await Task.WhenAll(
            new CustomerService(firstDb).CreateAsync(new CreateCustomerRequest { CustomerFullName = "First" }, 11),
            new CustomerService(secondDb).CreateAsync(new CreateCustomerRequest { CustomerFullName = "Second" }, 12));
        Assert.NotEqual(results[0].CustomerId, results[1].CustomerId);
        Assert.NotEqual(results[0].CustomerCode, results[1].CustomerCode);
        Assert.All(results, customer => Assert.Equal($"CUST-{customer.CustomerId:D6}", customer.CustomerCode));
        Assert.Equal(2, await firstDb.TblCustomers.CountAsync());
    }

    [Theory]
    [InlineData("CUST-000007")]
    [InlineData("ACME-01")]
    public async Task Update_PreservesExistingCodeEvenWhenPayloadAttemptsToReplaceIt(string code)
    {
        await using var db = CreateContext();
        db.TblCustomers.Add(new TblCustomer { CustomerId = 7, CustomerCode = code, CustomerFullName = "Original", Status = 1 });
        await db.SaveChangesAsync();
        var request = JsonSerializer.Deserialize<UpdateCustomerRequest>(
            """{"customerCode":"REPLACED","customerFullName":"Updated","customerContactPersonName":"Contact"}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var service = new CustomerService(db);
        await service.UpdateAsync(7, request, 12);
        db.ChangeTracker.Clear();
        var updated = await service.GetByIdAsync(7);
        Assert.Equal(code, updated.CustomerCode);
        Assert.Equal("Updated", updated.CustomerFullName);
        Assert.Equal("Contact", updated.CustomerContactPersonName);
        Assert.Equal(12, (await db.TblCustomers.SingleAsync()).UserModified);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Update_CustomerWithoutCodeGetsStableSystemCode(string? originalCode)
    {
        await using var db = CreateContext();
        db.TblCustomers.Add(new TblCustomer { CustomerId = 7, CustomerCode = originalCode, CustomerFullName = "Original", Status = 0 });
        await db.SaveChangesAsync();
        var service = new CustomerService(db);
        await service.UpdateAsync(7, new UpdateCustomerRequest { CustomerFullName = "Updated" }, 12);
        await service.UpdateAsync(7, new UpdateCustomerRequest { CustomerFullName = "Updated again" }, 12);
        var updated = await service.GetByIdAsync(7);
        Assert.Equal("CUST-000007", updated.CustomerCode);
        Assert.Equal((byte)0, updated.Status);
    }

    [Fact]
    public async Task Update_ConflictingCodeIsRejectedIncludingInactiveCustomers()
    {
        await using var db = CreateContext();
        db.TblCustomers.AddRange(
            new TblCustomer { CustomerId = 7, CustomerFullName = "Original", Status = 1 },
            new TblCustomer { CustomerId = 8, CustomerFullName = "Other", CustomerCode = "CUST-000007", Status = 0 });
        await db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => new CustomerService(db).UpdateAsync(
            7, new UpdateCustomerRequest { CustomerFullName = "Updated" }, 12));
        Assert.Equal("CustomerCodeConflict", error.Code);
        db.ChangeTracker.Clear();
        var unchanged = await db.TblCustomers.SingleAsync(x => x.CustomerId == 7);
        Assert.Null(unchanged.CustomerCode);
        Assert.Equal("Original", unchanged.CustomerFullName);
    }

    [Fact]
    public async Task Update_MissingCustomerStillReturnsNotFound()
    {
        await using var db = CreateContext();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => new CustomerService(db).UpdateAsync(
            99, new UpdateCustomerRequest { CustomerFullName = "Missing" }, 12));
        Assert.Empty(db.TblCustomers);
    }

    private static DbDtctechContext CreateContext(string? databaseName = null) => new(
        new DbContextOptionsBuilder<DbDtctechContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);
}
