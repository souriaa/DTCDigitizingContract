using ContractManagement.API.Common.Enums;
using ContractManagement.API.Domains.DTOs.Requests.Contract;
using Microsoft.EntityFrameworkCore;
using static ContractManagement.Tests.ContractRichTextTestData;

namespace ContractManagement.Tests.Domains.Services.Contract;

public sealed partial class ContractServiceSlice04Tests
{
    [Fact]
    public async Task Create_WithEditedAndCustomAppendices_PersistsExactContentsAndOrder()
    {
        await using var db = CreateContext();
        await SeedCreateDependenciesAsync(db);
        await SeedTemplateAppendicesAsync(db);
        var request = CreateAppendicesRequest();
        var requiredAppendix = request.Appendices![0];
        requiredAppendix.AppendixName = "Phụ lục đã sửa";
        requiredAppendix.Terms[0].TermTitle = "Điều khoản đã sửa";
        requiredAppendix.Terms[0].TermContent = RichText("Nội dung đã sửa");
        requiredAppendix.Terms.Add(new() { TermCode = "TERM_2", TermTitle = "Điều khoản mới", TermContent = RichText("Nội dung mới") });
        request.Appendices.Insert(0, new()
        {
            AppendixCode = " pl-new ", AppendixName = "Phụ lục riêng", AppendixDescription = "Mô tả riêng",
            Terms = [new() { TermCode = "NEW", TermTitle = "Điều khoản riêng", TermContent = RichText("Nội dung riêng") }]
        });
        var result = await CreateService(db).CreateAsync(request, EmployeeId);
        var appendices = await db.TblContractAppendices.OrderBy(x => x.DisplayOrder).ToListAsync();
        Assert.Equal(2, appendices.Count);
        Assert.All(appendices, x => { Assert.Equal(result.ContractId, x.ContractId); Assert.Equal(result.CurrentVersionId, x.VersionId); });
        Assert.Equal([1, 2], appendices.Select(x => x.DisplayOrder));
        Assert.Equal("PL-NEW", appendices[0].AppendixCode);
        Assert.Null(appendices[0].SourceTemplateAppendixId);
        Assert.False(appendices[0].IsRequired);
        Assert.Equal(65, appendices[1].SourceTemplateAppendixId);
        Assert.True(appendices[1].IsRequired);
        Assert.Equal("Phụ lục đã sửa", appendices[1].AppendixName);
        var terms = await db.TblContractAppendixTerms.Where(x => x.AppendixId == appendices[1].AppendixId).OrderBy(x => x.DisplayOrder).ToListAsync();
        Assert.Equal(2, terms.Count);
        Assert.Equal(67, terms[0].SourceTemplateAppendixTermId);
        Assert.Equal("Điều khoản đã sửa", terms[0].TermTitle);
        Assert.Equal(RichText("Nội dung đã sửa"), terms[0].TermContent);
        Assert.Null(terms[1].SourceTemplateAppendixTermId);
        Assert.Equal([1, 2], terms.Select(x => x.DisplayOrder));
        // Optional appendix 66 was not selected. Source template data is never edited.
        Assert.DoesNotContain(appendices, x => x.SourceTemplateAppendixId == 66);
        Assert.Equal("Phụ lục bắt buộc", (await db.TblContractTemplateAppendices.SingleAsync(x => x.TemplateAppendixId == 65)).AppendixName);
        Assert.Equal("Điều khoản bắt buộc", (await db.TblContractTemplateAppendixTerms.SingleAsync(x => x.TemplateAppendixTermId == 67)).TermTitle);
    }

    [Fact]
    public async Task Create_AppendixTermRemovalAndReordering_AreNotReplacedByTemplateTerms()
    {
        await using var db = CreateContext();
        await SeedCreateDependenciesAsync(db);
        await SeedTemplateAppendicesAsync(db);
        var request = CreateAppendicesRequest();
        request.Appendices![0].Terms = [
            new() { TermCode = "TERM_2", TermTitle = "Điều khoản thứ nhất" },
            new() { TermCode = "TERM_1", TermTitle = "Điều khoản thứ hai" }
        ];
        await CreateService(db).CreateAsync(request, EmployeeId);
        var terms = await db.TblContractAppendixTerms.OrderBy(x => x.DisplayOrder).ToListAsync();
        Assert.Equal(["TERM_2", "TERM_1"], terms.Select(x => x.TermCode));
        Assert.All(terms, x => Assert.Null(x.SourceTemplateAppendixTermId));
    }

    [Theory]
    [InlineData("missing-required")]
    [InlineData("foreign-appendix")]
    [InlineData("duplicate-source")]
    [InlineData("duplicate-code")]
    [InlineData("foreign-term")]
    [InlineData("custom-with-source-term")]
    [InlineData("duplicate-term-source")]
    [InlineData("duplicate-term-code")]
    [InlineData("empty-terms")]
    [InlineData("invalid-rich-text")]
    [InlineData("invalid-mode")]
    [InlineData("combined-in-vietnamese")]
    [InlineData("empty-name")]
    [InlineData("long-code")]
    [InlineData("empty-title")]
    [InlineData("mixed-payloads")]
    public async Task Create_InvalidAppendixDraft_RejectsBeforeWritingAggregate(string scenario)
    {
        await using var db = CreateContext();
        await SeedCreateDependenciesAsync(db);
        await SeedTemplateAppendicesAsync(db);
        var request = CreateAppendicesRequest();
        var appendix = request.Appendices![0];
        switch (scenario)
        {
            case "missing-required": request.Appendices.Clear(); break;
            case "foreign-appendix": appendix.SourceTemplateAppendixId = 999; break;
            case "duplicate-source": request.Appendices.Add(appendix); break;
            case "duplicate-code": request.Appendices.Add(new() { AppendixCode = " pl-req ", AppendixName = "Trùng mã", Terms = [new() { TermCode = "NEW", TermTitle = "Mới" }] }); break;
            case "foreign-term": appendix.Terms[0].SourceTemplateAppendixTermId = 68; break;
            case "custom-with-source-term": request.Appendices.Add(new() { AppendixCode = "CUSTOM", AppendixName = "Mới", Terms = [new() { SourceTemplateAppendixTermId = 67, TermCode = "NEW", TermTitle = "Mới" }] }); break;
            case "duplicate-term-source": appendix.Terms.Add(new() { SourceTemplateAppendixTermId = 67, TermCode = "OTHER", TermTitle = "Mới" }); break;
            case "duplicate-term-code": appendix.Terms.Add(new() { TermCode = " req-01 ", TermTitle = "Mới" }); break;
            case "empty-terms": appendix.Terms.Clear(); break;
            case "invalid-rich-text": appendix.Terms[0].TermContent = "not-rich-text"; break;
            case "invalid-mode": appendix.Terms[0].BilingualContentMode = 3; break;
            case "combined-in-vietnamese": appendix.Terms[0].BilingualContentMode = 2; break;
            case "empty-name": appendix.AppendixName = " "; break;
            case "long-code": appendix.AppendixCode = new string('A', 51); break;
            case "empty-title": appendix.Terms[0].TermTitle = " "; break;
            case "mixed-payloads": request.SelectedOptionalTemplateAppendixIds = [66]; break;
        }
        await Assert.ThrowsAnyAsync<ArgumentException>(() => CreateService(db).CreateAsync(request, EmployeeId));
        Assert.Empty(db.TblContracts);
        Assert.Empty(db.TblContractVersions);
        Assert.Empty(db.TblContractAppendices);
        Assert.Empty(db.TblContractAppendixTerms);
        Assert.Empty(db.TblContractItems);
    }

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    public async Task Create_BilingualAppendix_PreservesSeparateAndCombinedModes(byte mode)
    {
        await using var db = CreateContext();
        await SeedCreateDependenciesAsync(db);
        await SeedTemplateAppendicesAsync(db);
        (await db.TblContractTemplates.SingleAsync()).LanguageMode = (byte)ContractLanguageMode.Bilingual;
        await db.SaveChangesAsync();
        var request = CreateAppendicesRequest();
        request.LanguageMode = ContractLanguageMode.Bilingual;
        request.ContractNameEn = "Software agreement";
        request.Items[0].ItemNameEn = "Software";
        var appendix = request.Appendices![0];
        appendix.AppendixNameEn = "Implementation scope";
        appendix.Terms[0].TermTitleEn = "Delivery";
        appendix.Terms[0].BilingualContentMode = mode;
        appendix.Terms[0].TermContentEn = mode == 1 ? RichText("English content") : null;
        await CreateService(db).CreateAsync(request, EmployeeId);
        var saved = await db.TblContractAppendixTerms.SingleAsync();
        Assert.Equal(mode, saved.BilingualContentMode);
        Assert.Equal(appendix.Terms[0].TermContentEn, saved.TermContentEn);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("title")]
    [InlineData("body")]
    public async Task Create_BilingualAppendix_RequiresEnglishFields(string missing)
    {
        await using var db = CreateContext();
        await SeedCreateDependenciesAsync(db);
        await SeedTemplateAppendicesAsync(db);
        (await db.TblContractTemplates.SingleAsync()).LanguageMode = (byte)ContractLanguageMode.Bilingual;
        await db.SaveChangesAsync();
        var request = CreateAppendicesRequest();
        request.LanguageMode = ContractLanguageMode.Bilingual;
        request.ContractNameEn = "Software agreement";
        request.Items[0].ItemNameEn = "Software";
        var appendix = request.Appendices![0];
        appendix.AppendixNameEn = missing == "name" ? null : "Implementation scope";
        appendix.Terms[0].TermTitleEn = missing == "title" ? null : "Delivery";
        appendix.Terms[0].TermContentEn = missing == "body" ? null : RichText("English content");
        var error = await Assert.ThrowsAsync<ArgumentException>(() => CreateService(db).CreateAsync(request, EmployeeId));
        Assert.Contains("tiếng Anh", error.Message);
        Assert.Empty(db.TblContracts);
    }

    private static CreateContractRequest CreateAppendicesRequest()
    {
        var request = CreateRequest("VND", [CreateItem(ContractItemType.Product, 1, 100, sourceProductId: ProductId)]);
        request.Appendices = [new()
        {
            SourceTemplateAppendixId = 65, AppendixCode = "PL-REQ", AppendixName = "Phụ lục bắt buộc",
            Terms = [new() { SourceTemplateAppendixTermId = 67, TermCode = "REQ-01", TermTitle = "Điều khoản bắt buộc", TermContent = RichText("Nội dung bắt buộc") }]
        }];
        return request;
    }
}
