using ContractManagement.API.Domains.DTOs.Requests.LegalProfiles;
using ContractManagement.API.Domains.Services.LegalProfiles;
using ContractManagement.Infrastructure.Persistence.Application;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Tests.Domains.Services.Tenant;

public sealed class TenantLegalProfileServiceTests
{
    [Fact]
    public async Task ProfileWithPendingEnglish_ReadsAsNull_AndMustBeCompletedBeforeSaving()
    {
        await using var context = CreateContext();
        var service = new TenantLegalProfileService(context);
        await service.UpsertAsync(Request(), 11);
        var stored = await context.TblTenantLegalProfiles.SingleAsync();
        stored.LegalEntityNameEn = null;
        stored.AddressEn = null;
        stored.RepresentativeTitleEn = null;
        stored.BankNameEn = null;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var pending = await service.GetAsync();
        Assert.NotNull(pending);
        Assert.Null(pending.LegalEntityNameEn);
        Assert.Null(pending.AddressEn);
        Assert.Null(pending.RepresentativeTitleEn);
        Assert.Null(pending.BankNameEn);
        var incomplete = Request();
        incomplete.RowVersion = pending.RowVersion;
        incomplete.AddressEn = " ";
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpsertAsync(incomplete, 11));
        Assert.Null((await service.GetAsync())!.AddressEn);
        var complete = Request();
        complete.RowVersion = pending.RowVersion;
        var saved = await service.UpsertAsync(complete, 11);
        Assert.Equal("Hanoi", saved.AddressEn);
        Assert.Equal("DTC Company", saved.LegalEntityNameEn);
    }

    [Fact]
    public async Task Upsert_CreatesThenUpdatesSingletonProfile()
    {
        await using var context = CreateContext();
        var service = new TenantLegalProfileService(context);

        var created = await service.UpsertAsync(Request(), employeeId: 11);
        var updatedRequest = Request();
        updatedRequest.LegalEntityName = "Công ty TNHH DTC Mới";
        updatedRequest.RowVersion = created.RowVersion;

        var updated = await service.UpsertAsync(updatedRequest, employeeId: 12);

        Assert.Equal(1, await context.TblTenantLegalProfiles.CountAsync());
        Assert.Equal("Công ty TNHH DTC Mới", updated.LegalEntityName);
        Assert.Equal("02473000001", updated.PhoneNumber);
        Assert.Equal("02473000002", updated.FaxNumber);
        Assert.Equal("098765432109", updated.BankAccountNumber);
        Assert.Equal("Ngân hàng DTC", updated.BankName);
        Assert.Equal("DTC Company", updated.LegalEntityNameEn);
        Assert.Equal("Hanoi", updated.AddressEn);
        Assert.Equal("Director", updated.RepresentativeTitleEn);
        Assert.Equal("DTC Bank", updated.BankNameEn);
        Assert.Equal(11, updated.CreatedByEmployeeId);
        Assert.Equal(12, updated.UpdatedByEmployeeId);
    }

    [Fact]
    public async Task Upsert_RejectsStaleRowVersion()
    {
        await using var context = CreateContext();
        var service = new TenantLegalProfileService(context);
        await service.UpsertAsync(Request(), employeeId: 11);

        var stale = Request();
        stale.RowVersion = Convert.ToBase64String(new byte[8]);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            service.UpsertAsync(stale, employeeId: 12));
    }

    [Fact]
    public async Task SeparateTenantDatabases_DoNotShareLegalProfile()
    {
        await using var tenantA = CreateContext("tenant-a");
        await using var tenantB = CreateContext("tenant-b");
        await new TenantLegalProfileService(tenantA)
            .UpsertAsync(Request(), employeeId: 11);

        var otherTenantProfile = await new TenantLegalProfileService(tenantB)
            .GetAsync();

        Assert.Null(otherTenantProfile);
    }

    [Theory]
    [InlineData(nameof(UpsertTenantLegalProfileRequest.LegalEntityNameEn))]
    [InlineData(nameof(UpsertTenantLegalProfileRequest.AddressEn))]
    [InlineData(nameof(UpsertTenantLegalProfileRequest.RepresentativeTitleEn))]
    [InlineData(nameof(UpsertTenantLegalProfileRequest.BankNameEn))]
    public async Task EnglishFields_AreRequired_AndFailureDoesNotLeaveTrackedProfile(string field)
    {
        await using var context = CreateContext();
        var service = new TenantLegalProfileService(context);
        var request = Request();
        typeof(UpsertTenantLegalProfileRequest).GetProperty(field)!.SetValue(request, "   ");
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpsertAsync(request, 11));
        Assert.Empty(context.ChangeTracker.Entries());
        Assert.Empty(await context.TblTenantLegalProfiles.ToListAsync());
        await service.UpsertAsync(Request(), 11);
        Assert.Single(await context.TblTenantLegalProfiles.ToListAsync());
    }

    private static UpsertTenantLegalProfileRequest Request() => new()
    {
        LegalEntityName = "Công ty TNHH DTC",
        LegalEntityNameEn = " DTC Company ", AddressEn = " Hanoi ",
        RepresentativeTitleEn = " Director ", BankNameEn = " DTC Bank ",
        TaxCode = "0101234567",
        Address = "Hà Nội",
        RepresentativeName = "Nguyễn Văn A",
        RepresentativeTitle = "Giám đốc",
        PhoneNumber = "02473000001",
        FaxNumber = "02473000002",
        BankAccountNumber = "098765432109",
        BankName = "Ngân hàng DTC"
    };

    private static DbDtctechContext CreateContext(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<DbDtctechContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;
        return new DbDtctechContext(options);
    }
}
