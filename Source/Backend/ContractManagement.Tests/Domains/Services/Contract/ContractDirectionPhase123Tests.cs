using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ContractManagement.API.Common.Enums;
using ContractManagement.API.Domains.DTOs.Requests.Contract;
using ContractManagement.API.Domains.DTOs.Requests.ContractTemplate;
using ContractManagement.Domains.Policies.Contract;
using ContractManagement.Domains.Policies.ContractTemplate;
using ContractManagement.Domains.Services.ContractTemplate;
using ContractManagement.Infrastructure.Persistence.Application.Models;

namespace ContractManagement.Tests.Domains.Services.Contract;

public sealed class ContractDirectionPhase123Tests
{
    [Theory]
    [InlineData(ContractDirection.Sale, ContractPartySubject.Tenant, true)]
    [InlineData(ContractDirection.Purchase, ContractPartySubject.Counterparty, false)]
    public void Resolver_SeparatesIdentityFromRole(ContractDirection direction, ContractPartySubject provider, bool receives)
    {
        var tenant = new object(); var external = new object();
        var result = ContractPartyRoleResolver.Resolve(direction, tenant, external);
        Assert.Equal(provider, result.Provider.Subject);
        Assert.Same(direction == ContractDirection.Sale ? tenant : external, result.Provider.Value);
        Assert.Same(direction == ContractDirection.Sale ? external : tenant, result.Customer.Value);
        Assert.Equal(receives, result.TenantReceivesPayment);
    }

    [Theory]
    [InlineData(0)] [InlineData(3)] [InlineData(255)]
    public void Resolver_RejectsMissingOrUnknownDirection(byte direction) =>
        Assert.Throws<ArgumentException>(() => ContractPartyRoleResolver.Resolve((ContractDirection)direction, "tenant", "external"));

    [Fact]
    public void CreationPayloads_MustExplicitlySpecifyDirection()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CreateContractRequest>("{}"));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CreateContractTemplateRequest>("{}"));
        var errors = new List<ValidationResult>();
        var request = new CreateContractTemplateRequest { TemplateCode = "T", TemplateName = "Test" };
        Validator.TryValidateObject(request, new ValidationContext(request), errors, true);
        Assert.Contains(errors, x => x.MemberNames.Contains("ContractDirection"));
    }

    [Fact]
    public void Catalogs_UseSameOptionalRoleMarkers_AndHaveNoTenantOrCompanyAlias()
    {
        foreach (var fields in new[] { SoftwareSupplyPlaceholderCatalog.All, AcceptanceRecordPlaceholderCatalog.All, HandoverRecordPlaceholderCatalog.All })
        {
            Assert.DoesNotContain(fields, x => x.Key.StartsWith("TENANT_", StringComparison.Ordinal));
            Assert.DoesNotContain(fields, x => x.Key is "CUSTOMER_COMPANY" or "CUSTOMER_COMPANY_EN");
            Assert.Contains(fields, x => x.Key == "PROVIDER_COMPANY_NAME");
            Assert.Contains(fields, x => x.Key == "CUSTOMER_COMPANY_NAME");
            Assert.Contains(fields, x => x.Key == "CUSTOMER_REPRESENTATIVE_PHONE");
            Assert.DoesNotContain(fields, x => x.Key is "PROVIDER_LEGAL_NAME" or "PROVIDER_LEGAL_NAME_EN" or "CUSTOMER_LEGAL_NAME" or "CUSTOMER_LEGAL_NAME_EN");
            Assert.Equal(fields.Count, fields.Select(x => x.Key).Distinct().Count());
            Assert.All(fields, x => Assert.False(x.IsRequired));
        }
        Assert.Equal(DocumentPartyMarkers.Definitions.Select(x => x.Key),
            AcceptanceRecordPlaceholderCatalog.All.Where(x => DocumentPartyMarkers.Definitions.Any(p => p.Key == x.Key)).Select(x => x.Key));
    }

    [Theory]
    [InlineData(ContractDirection.Sale)] [InlineData(ContractDirection.Purchase)]
    public void CustomSources_DistinguishRoleAndSubject_WithoutSwitchingActors(ContractDirection direction)
    {
        var contract = new TblContract { ContractDirection = (byte)direction };
        var tenant = new TblTenantLegalProfile { LegalEntityName = "Tenant legal", BankName = "Tenant bank" };
        var external = new TblCustomer { CustomerCompany = "External legal", CustomerBankName = "External bank", CustomerContactPersonName = "Contact", CustomerRepresentativeName = "Not the contact" };
        var context = new ContractPlaceholderResolveContext(contract, new(), external, tenant, null, null);
        var registry = new ContractPlaceholderSourceRegistry();
        Assert.Equal("Tenant legal", registry.Resolve("tenant.legal-name", context));
        Assert.Equal("External legal", registry.Resolve("counterparty.company", context));
        Assert.Equal(direction == ContractDirection.Sale ? "Tenant bank" : "External bank", registry.Resolve("provider.bank-name", context));
        Assert.Equal(direction == ContractDirection.Sale ? "External bank" : "Tenant bank", registry.Resolve("customer.bank-name", context));
        Assert.Equal("Contact", registry.Resolve(direction == ContractDirection.Sale ? "customer.representative-name" : "provider.representative-name", context));
    }

    [Theory]
    [InlineData(ContractDirection.Sale, "CUSTOMER_ADDRESS_EN", "PROVIDER_ADDRESS_EN")]
    [InlineData(ContractDirection.Purchase, "PROVIDER_ADDRESS_EN", "CUSTOMER_ADDRESS_EN")]
    public void OptionalEnglish_FollowsExternalSubject_NotMarkerPrefix(ContractDirection direction, string optional, string required)
    {
        Assert.True(DocumentEnglishPolicy.IsOptionalPartyEnglish(DocumentPartyMarkers.Definitions.Single(x => x.Key == optional), direction));
        Assert.False(DocumentEnglishPolicy.IsOptionalPartyEnglish(DocumentPartyMarkers.Definitions.Single(x => x.Key == required), direction));
        var values = DocumentPartyMarkers.Values(direction, DocumentParty.From(new TblTenantLegalProfile { AddressEn = "Tenant English" }), DocumentParty.From(new TblCustomer { CustomerAddress = "VI only" }));
        Assert.Equal(string.Empty, values[optional]);
        Assert.Equal("Tenant English", values[required]);
    }
}

public sealed partial class ContractServiceSlice04Tests
{
    [Theory]
    [InlineData((ContractDirection)0)] [InlineData((ContractDirection)3)]
    public async Task Create_RejectsMissingOrInvalidDirection_EvenWithoutMvcValidation(ContractDirection direction)
    {
        await using var db = CreateContext(); await SeedCreateDependenciesAsync(db);
        var request = CreateRequest("VND", [CreateItem(ContractItemType.Product, 1, 100, sourceProductId: ProductId)]);
        request.ContractDirection = direction;
        await Assert.ThrowsAsync<ArgumentException>(() => CreateService(db).CreateAsync(request, EmployeeId));
        Assert.Empty(db.TblContracts);
    }

    [Fact]
    public async Task Create_MaintenanceRejectsParentWithOppositeDirection()
    {
        await using var db = CreateContext(); await SeedCreateDependenciesAsync(db);
        var service = CreateService(db);
        var source = await service.CreateAsync(CreateRequest("VND", [CreateItem(ContractItemType.Product, 1, 100, sourceProductId: ProductId)]), EmployeeId);
        db.TblContracts.Single(x => x.ContractId == source.ContractId).Status = (byte)ContractStatus.Completed;
        db.TblContractTemplates.Single().DocumentType = (byte)ContractManagement.Common.Enums.TemplateDocumentType.SoftwareMaintenanceContract;
        db.TblContractTemplateVersions.Single().ContractDirection = (byte)ContractDirection.Purchase;
        await db.SaveChangesAsync();
        var request = CreateRequest("VND", [CreateItem(ContractItemType.Product, 1, 100, sourceProductId: ProductId)]);
        request.ContractType = ContractType.SoftwareMaintenance;
        request.ContractDirection = ContractDirection.Purchase;
        request.ParentContractId = source.ContractId;
        db.TblContractTemplateFields.Add(new() { TemplateVersionId = TemplateVersionId,
            PlaceholderKey = "CONTRACT_LEGAL_BASES", FieldLabel = "Căn cứ", DataSource = "Contract.LegalBases" });
        await db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(request, EmployeeId));
        Assert.Contains("cùng chiều Mua/Bán", error.Message);
        Assert.Single(db.TblContracts);
    }

    [Theory]
    [InlineData(ContractStatus.Draft)] [InlineData(ContractStatus.Negotiating)]
    public async Task Update_CannotChangeDirection_AfterCreation(ContractStatus status)
    {
        await using var db = CreateContext(); await SeedCreateDependenciesAsync(db);
        var service = CreateService(db);
        var created = await service.CreateAsync(CreateRequest("VND", [CreateItem(ContractItemType.Product, 1, 100, sourceProductId: ProductId)]), EmployeeId);
        var contract = db.TblContracts.Single(x => x.ContractId == created.ContractId);
        contract.Status = (byte)status;
        await db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateDraftAsync(created.ContractId, new()
        {
            ContractDirection = ContractDirection.Purchase, CustomerId = CustomerId, ContractName = "Do not change direction",
            CurrencyCode = "VND", CurrentVersionId = created.CurrentVersionId, RowVersion = created.RowVersion,
            CurrentVersionRowVersion = created.CurrentVersionRowVersion,
            Items = [new() { ItemType = ContractItemType.Product, ItemName = "Item", Quantity = 1, UnitPrice = 100 }],
            Terms = [new() { TermCode = "TERM", TermTitle = "Title", TermKind = ContractTermKind.General }]
        }, EmployeeId));
        Assert.Contains("Không được đổi chiều", error.Message);
        Assert.Equal((byte)ContractDirection.Sale, db.TblContracts.Single(x => x.ContractId == created.ContractId).ContractDirection);
    }

    [Theory]
    [InlineData(ContractDirection.Sale)] [InlineData(ContractDirection.Purchase)]
    public async Task Create_PersistsExplicitDirection_WithMatchingTemplate(ContractDirection direction)
    {
        await using var db = CreateContext(); await SeedCreateDependenciesAsync(db);
        var template = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(db.TblContractTemplateVersions, x => x.TemplateVersionId == TemplateVersionId);
        template.ContractDirection = (byte)direction;
        await db.SaveChangesAsync();
        var request = CreateRequest("VND", [CreateItem(ContractItemType.Product, 1, 100, sourceProductId: ProductId)]);
        request.ContractDirection = direction;
        var created = await CreateService(db).CreateAsync(request, EmployeeId);
        Assert.Equal(direction, created.ContractDirection);
        Assert.Equal((byte)direction, db.TblContracts.Single(x => x.ContractId == created.ContractId).ContractDirection);
    }

    [Fact]
    public async Task Create_RejectsOppositeDirectionTemplate_WithoutCreatingContract()
    {
        await using var db = CreateContext(); await SeedCreateDependenciesAsync(db);
        var request = CreateRequest("VND", [CreateItem(ContractItemType.Product, 1, 100, sourceProductId: ProductId)]);
        request.ContractDirection = ContractDirection.Purchase;
        await Assert.ThrowsAsync<ArgumentException>(() => CreateService(db).CreateAsync(request, EmployeeId));
        Assert.Empty(db.TblContracts);
    }
}
