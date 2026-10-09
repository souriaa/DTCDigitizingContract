using System.Text.Json;
using ContractManagement.API.Domains.Services.Catalog;
using ContractManagement.Domains.Services.Catalog;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Tests.Domains.Services.Catalog;

public sealed class CatalogOptionsTests
{
    private static DbDtctechContext Context() => new(
        new DbContextOptionsBuilder<DbDtctechContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task ServiceOptions_OnlyReturnPickerFields_ExcludeDeleted_KeepInactiveAndHierarchy()
    {
        await using var db = Context();
        db.TblServices.AddRange(
            new TblService { ServiceId = 1, ServiceName = "B", Status = 1, ServiceContent = "Large content" },
            new TblService { ServiceId = 2, ServiceName = "A", Status = 0, ServiceParentId = 1 },
            new TblService { ServiceId = 3, ServiceName = "Deleted", Status = 2 });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var options = await new ServiceService(db).GetOptionsAsync();

        Assert.Equal(new[] { 2, 1 }, options.Select(x => x.ServiceId));
        Assert.Equal(1, options[0].ServiceParentId);
        Assert.Equal((byte)0, options[0].Status);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(options[0]));
        Assert.Equal(new[] { "ServiceId", "ServiceName", "ServiceParentId", "Status" },
            json.RootElement.EnumerateObject().Select(x => x.Name).OrderBy(x => x));
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task ServiceTypeOptions_OnlyReturnIdAndName_NoCountsOrLanguage()
    {
        await using var db = Context();
        db.TblServiceTypes.AddRange(
            new TblServiceType { ServiceTypeId = 1, ServiceTypeName = "B", LangId = 1 },
            new TblServiceType { ServiceTypeId = 2, ServiceTypeName = "A", LangId = 2 });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var options = await new ServiceTypeService(db).GetOptionsAsync();

        Assert.Equal(new byte[] { 2, 1 }, options.Select(x => x.ServiceTypeId));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(options[0]));
        Assert.Equal(new[] { "ServiceTypeId", "ServiceTypeName" },
            json.RootElement.EnumerateObject().Select(x => x.Name).OrderBy(x => x));
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Options_ReturnAllItems_NotLimitedToOnePage()
    {
        await using var db = Context();
        for (var id = 1; id <= 140; id++)
        {
            db.TblServices.Add(new TblService { ServiceId = id, ServiceName = $"Service {id}", Status = 1 });
            db.TblServiceTypes.Add(new TblServiceType { ServiceTypeId = (byte)id, ServiceTypeName = $"Type {id}" });
        }
        await db.SaveChangesAsync();

        Assert.Equal(140, (await new ServiceService(db).GetOptionsAsync()).Count);
        Assert.Equal(140, (await new ServiceTypeService(db).GetOptionsAsync()).Count);
    }

    [Fact]
    public async Task EmptyCatalog_ReturnsEmptyOptions()
    {
        await using var db = Context();
        Assert.Empty(await new ServiceService(db).GetOptionsAsync());
        Assert.Empty(await new ServiceTypeService(db).GetOptionsAsync());
    }
}
