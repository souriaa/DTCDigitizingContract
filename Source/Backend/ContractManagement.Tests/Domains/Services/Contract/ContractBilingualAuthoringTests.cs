using ContractManagement.API.Common.Enums;
using System.ComponentModel.DataAnnotations;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;
using static ContractManagement.Tests.ContractRichTextTestData;

namespace ContractManagement.Tests.Domains.Services.Contract;

public sealed partial class ContractServiceSlice04Tests
{
    [Theory]
    [InlineData((byte)1, true)]
    [InlineData((byte)2, false)]
    public async Task CreateRequestValidation_RequiresSeparateEnglishBodyOnlyInSeparateMode(byte mode, bool expectedError)
    {
        var request = CreateRequest("VND", [CreateItem(ContractItemType.Product, 1, 100, sourceProductId: ProductId)]);
        request.LanguageMode = ContractLanguageMode.Bilingual;
        request.ContractNameEn = "Software agreement";
        request.Terms = [new() { TermCode = "TERM_2", TermTitle = "Nội dung", TermTitleEn = "Content", TermContent = RichText("English / Tiếng Việt"), BilingualContentMode = mode }];
        var errors = request.Validate(new ValidationContext(request)).ToList();
        Assert.Equal(expectedError, errors.Any(error => error.ErrorMessage!.Contains("phải có nội dung tiếng Anh")));
        Assert.DoesNotContain(errors, error => error.ErrorMessage!.StartsWith("Item thứ"));
        if (!expectedError)
        {
            await using var db = CreateContext();
            await SeedCreateDependenciesAsync(db);
            (await db.TblContractTemplates.SingleAsync()).LanguageMode = (byte)ContractLanguageMode.Bilingual;
            await db.SaveChangesAsync();
            await CreateService(db).CreateAsync(request, EmployeeId);
            Assert.Equal(2, (await db.TblContractTerms.SingleAsync()).BilingualContentMode);
        }
    }

    [Theory]
    [InlineData(ContractItemType.Product, ContractLanguageMode.Vietnamese)]
    [InlineData(ContractItemType.Product, ContractLanguageMode.Bilingual)]
    [InlineData(ContractItemType.Service, ContractLanguageMode.Vietnamese)]
    [InlineData(ContractItemType.Service, ContractLanguageMode.Bilingual)]
    public async Task Create_CapturesEnglishNameFromCatalog_NotClientPayload_AndKeepsSnapshot(ContractItemType type, ContractLanguageMode language)
    {
        await using var db = CreateContext();
        await SeedCreateDependenciesAsync(db);
        (await db.TblContractTemplates.SingleAsync()).LanguageMode = (byte)language;
        await db.SaveChangesAsync();
        var request = CreateRequest("VND", [CreateItem(type, 1, 100,
            sourceProductId: type == ContractItemType.Product ? ProductId : null,
            sourceServiceId: type == ContractItemType.Service ? ServiceId : null)]);
        request.LanguageMode = language;
        request.ContractNameEn = "Software agreement";
        request.Items[0].ItemNameEn = "Client supplied name must not be used";
        await CreateService(db).CreateAsync(request, EmployeeId);
        var expected = type == ContractItemType.Product ? "Catalog product" : "Catalog service";
        Assert.Equal(expected, (await db.TblContractItems.SingleAsync()).ItemNameEn);
        (await db.TblProducts.SingleAsync()).ProductNameEn = "Changed product catalog";
        (await db.TblServices.SingleAsync()).ServiceNameEn = "Changed service catalog";
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.Equal(expected, (await db.TblContractItems.SingleAsync()).ItemNameEn);
    }

    [Theory]
    [InlineData(ContractItemType.Product)]
    [InlineData(ContractItemType.Service)]
    public async Task Create_BilingualCatalogItem_RejectsMissingMasterEnglish_EvenIfClientSuppliesName(ContractItemType type)
    {
        await using var db = CreateContext();
        await SeedCreateDependenciesAsync(db);
        (await db.TblContractTemplates.SingleAsync()).LanguageMode = (byte)ContractLanguageMode.Bilingual;
        if (type == ContractItemType.Product) (await db.TblProducts.SingleAsync()).ProductNameEn = " ";
        else (await db.TblServices.SingleAsync()).ServiceNameEn = " ";
        await db.SaveChangesAsync();
        var request = CreateRequest("VND", [CreateItem(type, 1, 100,
            sourceProductId: type == ContractItemType.Product ? ProductId : null,
            sourceServiceId: type == ContractItemType.Service ? ServiceId : null)]);
        request.LanguageMode = ContractLanguageMode.Bilingual;
        request.ContractNameEn = "Software agreement";
        request.Items[0].ItemNameEn = "Do not substitute a missing catalog translation";
        var error = await Assert.ThrowsAsync<ArgumentException>(() => CreateService(db).CreateAsync(request, EmployeeId));
        Assert.Contains("Tên tiếng Anh", error.Message);
        Assert.Empty(db.TblContracts);
    }

    [Fact]
    public async Task Create_BilingualCombinedTerm_PersistsAndReturnsMode()
    {
        await using var db = CreateContext();
        await SeedCreateDependenciesAsync(db);
        (await db.TblContractTemplates.SingleAsync()).LanguageMode = (byte)ContractLanguageMode.Bilingual;
        await db.SaveChangesAsync();
        var request = CreateRequest("VND", [CreateItem(ContractItemType.Product, 1, 100, sourceProductId: ProductId)]);
        request.LanguageMode = ContractLanguageMode.Bilingual;
        request.ContractNameEn = "Software supply";
        request.Items[0].ItemNameEn = "Software";
        request.Terms = [new()
        {
            TermCode = "COMBINED", TermTitle = "Nội dung", TermTitleEn = "Content",
            TermContent = RichText("English / Tiếng Việt"), BilingualContentMode = 2, DisplayOrder = 1
        }];
        await CreateService(db).CreateAsync(request, EmployeeId);
        var row = await db.TblContractTerms.SingleAsync();
        Assert.Equal(2, row.BilingualContentMode);
        Assert.Null(row.TermContentEn);
    }

    [Fact]
    public async Task Readiness_OptionalCustomerEnglish_DoesNotBlockBeforeOrAfterMasterUpdate()
    {
        await using var db = CreateContext();
        var versionId = await SeedNegotiatingContractAsync(db);
        var version = await db.TblContractVersions.SingleAsync(x => x.VersionId == versionId);
        db.TblContractTemplateFields.Add(new()
        {
            TemplateVersionId = version.TemplateVersionId, PlaceholderKey = "CUSTOMER_COMPANY_NAME_EN",
            FieldLabel = "Công ty khách hàng tiếng Anh", DataSource = "Customer.CustomerCompanyEn",
            SourceFieldKey = "system.CUSTOMER_COMPANY_NAME_EN", IsSystem = true, DataKind = 1, Multiplicity = 2
        });
        await db.SaveChangesAsync();
        var before = await CreateService(db).GetDetailAsync(100, EmployeeId);
        Assert.DoesNotContain(before.ApprovalReadiness.Blockers, x => x.Code == "EnglishSourceMissingCustomer");
        (await db.TblCustomers.SingleAsync()).CustomerCompanyEn = "Real English customer name";
        await db.SaveChangesAsync();
        var after = await CreateService(db).GetDetailAsync(100, EmployeeId);
        Assert.DoesNotContain(after.ApprovalReadiness.Blockers, x => x.Code.StartsWith("EnglishSourceMissing"));
        Assert.Equal(ContractStatus.Negotiating, after.Status);
        Assert.Equal(before.TotalAmount, after.TotalAmount);
    }

    [Fact]
    public async Task NegotiationRound_CopiesCombinedTermMode()
    {
        await using var db = CreateContext();
        var versionId = await SeedNegotiatingContractAsync(db);
        (await db.TblContracts.SingleAsync()).LanguageMode = (byte)ContractLanguageMode.Bilingual;
        var term = await db.TblContractTerms.SingleAsync();
        term.BilingualContentMode = 2;
        term.TermTitleEn = "Content";
        term.TermContent = RichText("English / Tiếng Việt");
        term.TermContentEn = null;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var response = await CreateService(db).CreateNegotiationRoundAsync(100,
            CreateRoundRequest(versionId), EmployeeId);
        var copy = await db.TblContractTerms.SingleAsync(x => x.VersionId == response.CurrentVersion.VersionId);
        Assert.Equal(2, copy.BilingualContentMode);
        Assert.Equal(term.TermContent, copy.TermContent);
        Assert.Null(copy.TermContentEn);
        var detail = await CreateService(db).GetDetailAsync(100, EmployeeId);
        Assert.Equal(2, Assert.Single(detail.CurrentVersion.Terms).BilingualContentMode);
    }
}

public partial class ContractServiceResponsibilityTransferTests
{
    [Fact]
    public async Task UpdateDraft_BilingualCombinedTerm_DoesNotRequireSeparateEnglishBody()
    {
        await using var db = CreateContext();
        await SeedContractAsync(db);
        (await db.TblContracts.SingleAsync()).LanguageMode = (byte)ContractLanguageMode.Bilingual;
        await db.SaveChangesAsync();
        var request = CreateUpdateRequest();
        request.ContractNameEn = "Software supply";
        foreach (var item in request.Items) item.ItemNameEn = "Software";
        request.Terms[0].BilingualContentMode = 2;
        request.Terms[0].TermTitleEn = "Content";
        request.Terms[0].TermContent = RichText("English / Tiếng Việt");
        request.Terms[0].TermContentEn = null;
        await CreateService(db).UpdateDraftAsync(ContractId, request, CurrentResponsibleEmployeeId);
        var term = await db.TblContractTerms.SingleAsync();
        Assert.Equal(2, term.BilingualContentMode);
        Assert.Null(term.TermContentEn);
    }
}
