using ContractManagement.API.Common.Enums;
using ContractManagement.API.Domains.DTOs.Requests.Contract;
using ContractManagement.Domains.Interfaces.Contract;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;
using static ContractManagement.Tests.ContractRichTextTestData;

namespace ContractManagement.Tests.Domains.Services.Contract;

public partial class ContractServiceResponsibilityTransferTests
{
    private const int EditableLegalBasisId = 7801;

    [Theory]
    [InlineData(ContractStatus.Draft)]
    [InlineData(ContractStatus.Negotiating)]
    public async Task LegalBasis_CanEditVersionContentAndAuditWithoutChangingTemplate(ContractStatus status)
    {
        await using var db = CreateContext(); await SeedContractAsync(db); await SeedEditableLegalBasis(db);
        (await db.TblContracts.SingleAsync()).Status = (byte)status;
        db.TblContractTemplateLegalBases.Add(new() {
            TemplateLegalBasisId = 7800, TemplateVersionId = 1, BasisCode = "LAW",
            ContentVi = RichText("Nội dung mẫu gốc"), DisplayOrder = 1,
            CreatedEmployeeId = CurrentResponsibleEmployeeId, CreatedDate = DateTime.UtcNow, RowVersion = InitialRowVersion() });
        await db.SaveChangesAsync();
        var request = CreateUpdateRequest();
        request.LegalBases = [LegalBasisUpdate()];
        var detail = await CreateService(db).UpdateDraftAsync(ContractId, request, CurrentResponsibleEmployeeId);
        var basis = Assert.Single(detail.CurrentVersion.LegalBases);
        Assert.Equal(RichText("Căn cứ đã sửa"), basis.ContentVi);
        Assert.Equal("LAW", basis.BasisCode);
        Assert.Equal(7800, basis.SourceTemplateLegalBasisId);
        Assert.Equal(RichText("Nội dung mẫu gốc"), (await db.TblContractTemplateLegalBases.SingleAsync()).ContentVi);
        var persisted = await db.TblContractLegalBases.SingleAsync();
        Assert.Equal(CurrentResponsibleEmployeeId, persisted.UpdatedEmployeeId);
        Assert.NotNull(persisted.UpdatedDate);
        if (status == ContractStatus.Negotiating)
        {
            var history = await CreateService(db).GetVersionDetailAsync(ContractId, VersionId, CurrentResponsibleEmployeeId);
            Assert.Equal(basis.ContentVi, Assert.Single(history.LegalBases).ContentVi);
        }
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateService(db).GetVersionDetailAsync(ContractId, VersionId, CurrentResponsibleEmployeeId));
        }
        var audit = await db.TblContractAudits.SingleAsync(x => x.ActionType == ContractAuditActionTypes.DraftUpdated);
        Assert.Contains("LAW", audit.Reason);
    }

    [Fact]
    public async Task LegalBasis_OmittedUpdatesDoNotDeleteOrOverwriteStoredBases()
    {
        await using var db = CreateContext(); await SeedContractAsync(db); await SeedEditableLegalBasis(db);
        await CreateService(db).UpdateDraftAsync(ContractId, CreateUpdateRequest(), CurrentResponsibleEmployeeId);
        Assert.Equal(RichText("Căn cứ ban đầu"), (await db.TblContractLegalBases.SingleAsync()).ContentVi);
    }

    [Theory]
    [InlineData("foreign-id")]
    [InlineData("other-version")]
    [InlineData("other-contract")]
    [InlineData("duplicate")]
    [InlineData("empty-vi")]
    [InlineData("invalid-rich-text")]
    public async Task LegalBasis_RejectsInvalidOrOutOfScopeUpdates(string scenario)
    {
        await using var db = CreateContext(); await SeedContractAsync(db); await SeedEditableLegalBasis(db);
        var request = CreateUpdateRequest();
        request.LegalBases = [LegalBasisUpdate()];
        switch (scenario)
        {
            case "foreign-id": request.LegalBases[0].LegalBasisId = 999; break;
            case "other-version": (await db.TblContractLegalBases.SingleAsync()).VersionId = 999; break;
            case "other-contract": (await db.TblContractLegalBases.SingleAsync()).ContractId = 999; break;
            case "duplicate": request.LegalBases.Add(LegalBasisUpdate()); break;
            case "empty-vi": request.LegalBases[0].ContentVi = " "; break;
            case "invalid-rich-text": request.LegalBases[0].ContentVi = "không phải rich text"; break;
        }
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => CreateService(db).UpdateDraftAsync(ContractId, request, CurrentResponsibleEmployeeId));
    }

    [Fact]
    public async Task LegalBasis_RejectsStaleRowVersion()
    {
        await using var db = CreateContext(); await SeedContractAsync(db); await SeedEditableLegalBasis(db);
        var request = CreateUpdateRequest(); request.LegalBases = [LegalBasisUpdate()];
        request.LegalBases[0].RowVersion = Convert.ToBase64String(NewerRowVersion());
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => CreateService(db).UpdateDraftAsync(ContractId, request, CurrentResponsibleEmployeeId));
    }

    [Theory]
    [InlineData(ContractStatus.PendingApproval)]
    [InlineData(ContractStatus.Signed)]
    public async Task LegalBasis_CannotEditOutsideAuthoring(ContractStatus status)
    {
        await using var db = CreateContext(); await SeedContractAsync(db); await SeedEditableLegalBasis(db);
        (await db.TblContracts.SingleAsync()).Status = (byte)status;
        await db.SaveChangesAsync();
        var request = CreateUpdateRequest(); request.LegalBases = [LegalBasisUpdate()];
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService(db).UpdateDraftAsync(ContractId, request, CurrentResponsibleEmployeeId));
    }

    [Fact]
    public async Task LegalBasis_BilingualContractRequiresEnglishContent()
    {
        await using var db = CreateContext(); await SeedContractAsync(db); await SeedEditableLegalBasis(db);
        (await db.TblContracts.SingleAsync()).LanguageMode = (byte)ContractLanguageMode.Bilingual;
        await db.SaveChangesAsync();
        var request = CreateUpdateRequest(); request.ContractNameEn = "Contract";
        request.Items[0].ItemNameEn = "Product";
        request.Terms[0].TermTitleEn = "General"; request.Terms[0].TermContentEn = RichText("General content");
        request.LegalBases = [LegalBasisUpdate()];
        await Assert.ThrowsAsync<ArgumentException>(() => CreateService(db).UpdateDraftAsync(ContractId, request, CurrentResponsibleEmployeeId));
        request.LegalBases[0].ContentEn = RichText("Updated legal basis");
        var detail = await CreateService(db).UpdateDraftAsync(ContractId, request, CurrentResponsibleEmployeeId);
        Assert.Equal(request.LegalBases[0].ContentEn, Assert.Single(detail.CurrentVersion.LegalBases).ContentEn);
    }

    private static UpdateContractLegalBasisRequest LegalBasisUpdate() => new() {
        LegalBasisId = EditableLegalBasisId, RowVersion = Convert.ToBase64String(InitialRowVersion()),
        ContentVi = RichText("Căn cứ đã sửa") };

    private static async Task SeedEditableLegalBasis(DbDtctechContext db)
    {
        db.TblContractLegalBases.Add(new TblContractLegalBasis {
            LegalBasisId = EditableLegalBasisId, ContractId = ContractId, VersionId = VersionId,
            SourceTemplateLegalBasisId = 7800, BasisCode = "LAW", ContentVi = RichText("Căn cứ ban đầu"), DisplayOrder = 1,
            CreatedEmployeeId = CurrentResponsibleEmployeeId, CreatedDate = DateTime.UtcNow, RowVersion = InitialRowVersion() });
        await db.SaveChangesAsync();
    }
}
