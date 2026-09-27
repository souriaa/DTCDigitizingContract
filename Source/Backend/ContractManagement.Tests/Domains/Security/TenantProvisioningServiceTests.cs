using System.ComponentModel.DataAnnotations;
using ContractManagement.Contracts.Tenants;
using ContractManagement.Infrastructure.DatabaseScripts.SeedData;
using ContractManagement.Infrastructure.MultiTenancy.Contracts;
using ContractManagement.Infrastructure.MultiTenancy.Enums;
using ContractManagement.Infrastructure.MultiTenancy.Interfaces;
using ContractManagement.Infrastructure.MultiTenancy.Options;
using ContractManagement.Infrastructure.MultiTenancy.Services;
using ContractManagement.Infrastructure.Persistence.Central;
using ContractManagement.Infrastructure.Persistence.Central.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ContractManagement.Tests.Domains.Security;

public sealed class TenantProvisioningServiceTests
{
    [Fact]
    public async Task Provisioning_ActivatesTenantOnlyAfterInitialManagerSeedAndWritesCentralAudit()
    {
        await using var centralDbContext = CreateCentralDbContext();
        var initializer = new RecordingInitializer();
        var seedData = new RecordingSeedData();
        var service = CreateService(centralDbContext, initializer, seedData);
        var command = NewCommand("tenant-a");

        var result = await service.CreateDedicatedAsync(command);

        Assert.Equal(TenantStatus.Active, result.Status);
        Assert.True(initializer.WasCalled);
        Assert.NotNull(seedData.InitialManager);
        Assert.Equal("first-manager", seedData.InitialManager!.EmployeeAccount);
        var audit = await centralDbContext.SecurityAudits.SingleAsync();
        Assert.Equal("TenantProvisioned", audit.Action);
        Assert.Equal("Success", audit.Result);
        Assert.Equal((byte)6, audit.NewEmployeeType);
        Assert.Equal((byte)1, audit.NewStatus);
    }

    [Fact]
    public async Task ProvisioningFailure_LeavesTenantFailedAndWritesFailedCentralAudit()
    {
        await using var centralDbContext = CreateCentralDbContext();
        var service = CreateService(
            centralDbContext,
            new RecordingInitializer(),
            new RecordingSeedData(new InvalidOperationException("seed failure")));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateDedicatedAsync(NewCommand("tenant-b")));

        var tenant = await centralDbContext.Tenants.SingleAsync();
        Assert.Equal(TenantStatus.Failed, tenant.Status);
        Assert.Contains("seed failure", tenant.ProvisioningError);
        var audit = await centralDbContext.SecurityAudits.SingleAsync();
        Assert.Equal("Failed", audit.Result);
        Assert.Equal("ProvisioningFailed", audit.FailureCode);
    }

    [Fact]
    public async Task Provisioning_PreservesMixedCaseCodeAndUsesNormalizedDatabaseIdentity()
    {
        await using var centralDbContext = CreateCentralDbContext();
        var service = CreateService(
            centralDbContext, new RecordingInitializer(), new RecordingSeedData());

        var result = await service.CreateDedicatedAsync(NewCommand("DtcTech"));

        Assert.Equal("DtcTech", result.TenantCode);
        var tenant = await centralDbContext.Tenants
            .Include(item => item.TenantDatabase).SingleAsync();
        Assert.Equal("DtcTech", tenant.TenantCode);
        Assert.Equal("dedicated-dtctech", tenant.TenantDatabase.DatabaseKey);
        Assert.Equal("ContractManagement_Tenant_dtctech",
            tenant.TenantDatabase.DatabaseName);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateDedicatedAsync(NewCommand("dtctech")));
    }

    [Fact]
    public async Task Provisioning_AcceptsTenantNameWithFiveHundredCharacters()
    {
        await using var centralDbContext = CreateCentralDbContext();
        var service = CreateService(
            centralDbContext, new RecordingInitializer(), new RecordingSeedData());
        var command = NewCommand("long-name") with { TenantName = new string('Â', 500) };

        await service.CreateDedicatedAsync(command);

        Assert.Equal(command.TenantName,
            (await centralDbContext.Tenants.SingleAsync()).TenantName);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateDedicatedAsync(NewCommand("too-long") with
            {
                TenantName = new string('Â', 501)
            }));
    }

    [Fact]
    public async Task CentralModel_UsesCaseInsensitiveTenantCodeAndLongTenantName()
    {
        await using var centralDbContext = CreateCentralDbContext();
        var entity = centralDbContext.GetService<IDesignTimeModel>().Model
            .FindEntityType(typeof(Tenant))!;

        Assert.Equal("Latin1_General_100_CI_AS",
            entity.FindProperty(nameof(Tenant.TenantCode))!.GetCollation());
        Assert.Equal(500,
            entity.FindProperty(nameof(Tenant.TenantName))!.GetMaxLength());
    }

    [Fact]
    public void CreateTenantRequest_ValidatesMixedCaseCodeAndFiveHundredCharacterName()
    {
        var request = new CreateTenantRequest
        {
            TenantCode = "DtcTech",
            TenantName = new string('Â', 500),
            InitialManager = new InitialManagerRequest()
        };
        var results = new List<ValidationResult>();

        Assert.True(Validator.TryValidateObject(request,
            new ValidationContext(request), results, true));

        request.TenantName += "Â";
        Assert.False(Validator.TryValidateObject(request,
            new ValidationContext(request), results, true));
    }

    private static TenantProvisioningService CreateService(
        CentralDbContext centralDbContext,
        ITenantDatabaseInitializer initializer,
        ITenantSeedData seedData)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:TenantDatabaseTemplate"] =
                    "Server=(localdb)\\MSSQLLocalDB;Initial Catalog=template;Integrated Security=True;TrustServerCertificate=True"
            })
            .Build();

        return new TenantProvisioningService(
            centralDbContext,
            initializer,
            Options.Create(new MultiTenancyOptions
            {
                DatabasePrefix = "ContractManagement_Tenant_",
                TemplateConnectionName = "TenantDatabaseTemplate"
            }),
            configuration,
            NullLogger<TenantProvisioningService>.Instance,
            seedData);
    }

    private static TenantProvisioningCommand NewCommand(string tenantCode) => new(
        tenantCode,
        "Tenant Name",
        new InitialManagerProvisioningCommand(
            "MGR-001",
            "first-manager",
            "Password123!",
            "First Manager",
            null,
            null),
        new SecurityOperationContext(
            1,
            "127.0.0.1",
            "unit-test",
            "provisioning-test"));

    private static CentralDbContext CreateCentralDbContext()
    {
        return new CentralDbContext(
            new DbContextOptionsBuilder<CentralDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
    }

    private sealed class RecordingInitializer : ITenantDatabaseInitializer
    {
        public bool WasCalled { get; private set; }

        public Task InitializeAsync(
            string connectionString,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingSeedData : ITenantSeedData
    {
        private readonly Exception? _exception;

        public RecordingSeedData(Exception? exception = null)
        {
            _exception = exception;
        }

        public InitialManagerProvisioningCommand? InitialManager { get; private set; }

        public Task InitializeAsync(
            string connectionString,
            int tenantId,
            InitialManagerProvisioningCommand initialManager,
            SecurityOperationContext securityContext,
            CancellationToken cancellationToken = default)
        {
            InitialManager = initialManager;
            return _exception is null
                ? Task.CompletedTask
                : Task.FromException(_exception);
        }
    }
}
