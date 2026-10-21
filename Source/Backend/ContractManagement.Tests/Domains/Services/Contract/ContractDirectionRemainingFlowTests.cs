using ContractManagement.API.Common.Enums;
using ContractManagement.API.Common.Exceptions;
using ContractManagement.API.Domains.DTOs.Requests.Contract;
using ContractManagement.API.Domains.Models.Contract;
using ContractManagement.Domains.Policies.ContractTemplate;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Tests.Domains.Services.Contract;

public sealed partial class ContractAcceptanceServicePhase7Tests
{
    [Fact]
    public async Task BilingualCreate_RequiresEnglishLocation_EvenWithoutLocationMarker()
    {
        await using var db = CreateContext();
        var storage = new TrackingStorage();
        await SeedAsync(db, storage, language: ContractLanguageMode.Bilingual);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateService(db, storage).CreateAsync(ContractId, CreateRequest(), EmployeeId));
        Assert.Equal("AcceptanceLocationEnRequired", error.Code);
        Assert.Empty(await db.TblContractAcceptanceRecords.ToListAsync());
    }

    [Fact]
    public async Task Update_CannotRenameEitherLegalParty()
    {
        await using var db = CreateContext();
        var storage = new TrackingStorage();
        await SeedAsync(db, storage);
        var service = CreateService(db, storage);
        var draft = await service.CreateAsync(ContractId, CreateRequest(), EmployeeId);
        var tenant = draft.Parties.Single(x => x.PartyRole == AcceptancePartyRole.Tenant);
        var external = draft.Parties.Single(x => x.PartyRole == AcceptancePartyRole.Customer);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => service.UpdateAsync(ContractId, draft.AcceptanceRecordId,
            new UpdateContractAcceptanceRecordRequest {
                AcceptanceCode = draft.AcceptanceCode, AcceptanceDate = draft.AcceptanceDate,
                Location = draft.Location, AcceptanceKind = draft.AcceptanceKind, RowVersion = draft.RowVersion,
                CustomerParty = new() { LegalName = "Different company", RepresentativeName = external.RepresentativeName },
                TenantParty = new() { LegalName = tenant.LegalName, RepresentativeName = tenant.RepresentativeName }
            }, EmployeeId));
        Assert.Equal("AcceptanceLegalNameImmutable", error.Code);
    }

    [Theory]
    [InlineData(ContractDirection.Sale)]
    [InlineData(ContractDirection.Purchase)]
    public async Task Create_UsesFrozenContactsAndDate_NotCurrentPartyMaster(ContractDirection direction)
    {
        await using var db = CreateContext();
        var storage = new TrackingStorage();
        await SeedAsync(db, storage, direction: direction);
        var customer = await db.TblCustomers.SingleAsync();
        customer.CustomerContactPersonName = "Changed master contact";
        customer.CustomerCompany = "Changed master legal name";
        await db.SaveChangesAsync();
        var service = CreateService(db, storage);
        var draft = await service.CreateAsync(ContractId, CreateRequest(), EmployeeId);
        Assert.Equal(direction, draft.ContractDirection);
        Assert.Equal("Người liên hệ nghiệm thu", draft.Parties.Single(x => x.PartyRole == AcceptancePartyRole.Customer).RepresentativeName);
        Assert.DoesNotContain("Changed master", string.Join(" ", draft.References.Select(x => x.TextVi)));
        (await db.TblContracts.SingleAsync()).SignDate = new DateTime(2026, 9, 15);
        await db.SaveChangesAsync();
        var reloaded = await service.GetAsync(ContractId, draft.AcceptanceRecordId, EmployeeId);
        Assert.Contains("ký ngày 12/09/2026", reloaded.References[0].TextVi);
        Assert.Equal(draft.References[0].TextVi, reloaded.References[0].TextVi);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Templates_RejectOppositeDirectionOrLanguage(bool directionMismatch)
    {
        await using var db = CreateContext();
        var storage = new TrackingStorage();
        await SeedAsync(db, storage);
        if (directionMismatch)
            (await db.TblContractTemplateVersions.SingleAsync()).ContractDirection = (byte)ContractDirection.Purchase;
        else
            (await db.TblContractTemplates.SingleAsync()).LanguageMode = (byte)ContractLanguageMode.Bilingual;
        await db.SaveChangesAsync();
        var service = CreateService(db, storage);
        Assert.Empty(await service.GetTemplatesAsync(ContractId, EmployeeId));
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(ContractId, CreateRequest(), EmployeeId));
        Assert.Equal("AcceptanceTemplateUnavailable", error.Code);
        Assert.Empty(await db.TblContractAcceptanceRecords.ToListAsync());
    }

    [Fact]
    public async Task Purchase_CustomRoleUsesEditedTenant_AndFrozenCounterpartyBank()
    {
        await using var db = CreateContext();
        var storage = new TrackingStorage();
        await SeedAsync(db, storage, direction: ContractDirection.Purchase, counterpartyBank: "Frozen supplier bank");
        var frozen = await db.TblContractVersionPartySnapshots.SingleAsync(x => x.PartyRole == 2);
        (await db.TblCustomers.SingleAsync()).CustomerBankName = "Changed master bank";
        db.TblContractTemplateFields.Add(new()
        {
            TemplateVersionId = TemplateVersionId, PlaceholderKey = "ACCEPTANCE_CONTACT",
            FieldLabel = "Buyer representative", SourceFieldKey = "customer.representative-name",
            DataSource = "Buyer", DataKind = 1, Multiplicity = 2, IsSystem = false
        });
        await db.SaveChangesAsync();
        storage.Add("templates/acceptance.docx", CreateTemplateDocxWithCustomAndSignatures());
        var service = CreateService(db, storage);
        var draft = await service.CreateAsync(ContractId, CreateRequest(), EmployeeId);
        var internalParty = await db.TblContractAcceptanceParties.SingleAsync(x => x.PartyRole == (byte)AcceptancePartyRole.Tenant);
        internalParty.RepresentativeName = "Edited tenant buyer";
        await db.SaveChangesAsync();
        var result = await service.PreviewAsync(ContractId, draft.AcceptanceRecordId, "docx", EmployeeId);
        using var document = WordprocessingDocument.Open(new MemoryStream(result.Content), false);
        Assert.Contains("Edited tenant buyer", document.MainDocumentPart!.Document!.InnerText);
        var values = DocumentPartyMarkers.Values(ContractDirection.Purchase,
            DocumentParty.From(FrozenContractPartySources.Tenant(await db.TblContractVersionPartySnapshots.SingleAsync(x => x.PartyRole == 1))),
            DocumentParty.From(FrozenContractPartySources.Counterparty(frozen)));
        Assert.Equal("Frozen supplier bank", values["PROVIDER_BANK_NAME"]);
    }


}

public sealed partial class ContractDocumentPreviewServiceTests
{
    [Theory]
    [InlineData(ContractDirection.Sale)]
    [InlineData(ContractDirection.Purchase)]
    public async Task Submit_ResolvesRolesFromOneSnapshot_AndDirectionChangesHash(ContractDirection direction)
    {
        await using var db = CreateContext();
        var source = CreateSourceDocument();
        await SeedAsync(db, source);
        (await db.TblContracts.SingleAsync()).ContractDirection = (byte)direction;
        (await db.TblContractTemplateVersions.SingleAsync()).ContractDirection = (byte)direction;
        await db.SaveChangesAsync();
        var pdf = new CapturingPdfRenderer();
        var result = await CreateService(db, source, pdf).RenderAsync(ContractId, OwnerId);
        var values = DocumentPartyMarkers.Values(direction, DocumentParty.From(result.Snapshot.Tenant), DocumentParty.From(result.Snapshot.Customer));
        Assert.Equal(direction == ContractDirection.Sale ? result.Snapshot.Tenant.LegalEntityName : result.Snapshot.Customer.LegalName, values["PROVIDER_COMPANY_NAME"]);
        Assert.Equal(direction == ContractDirection.Purchase ? result.Snapshot.Tenant.LegalEntityName : result.Snapshot.Customer.LegalName, values["CUSTOMER_COMPANY_NAME"]);
        Assert.Equal(result.DocxContent, pdf.InputDocx);
        using var document = WordprocessingDocument.Open(new MemoryStream(result.DocxContent), false);
        Assert.DoesNotContain("{{", document.MainDocumentPart!.Document!.InnerText);
        var originalHash = SoftwareSupplyContractSnapshotFactory.CalculateHash(result.Snapshot);
        var reversed = result.Snapshot with { Contract = result.Snapshot.Contract with { ContractDirection = direction == ContractDirection.Sale ? (byte)ContractDirection.Purchase : (byte)ContractDirection.Sale } };
        Assert.NotEqual(originalHash, SoftwareSupplyContractSnapshotFactory.CalculateHash(reversed));
        var persisted = SoftwareSupplyContractSnapshotFactory.CreatePersistenceGraph(result.Snapshot, OwnerId, DateTime.UtcNow);
        Assert.Equal((byte)direction, persisted.ContractDirection);
        Assert.Equal(result.Snapshot.Customer.ContactName, persisted.Parties.Single(x => x.PartyRole == 2).ContactName);
    }
}
