using ContractManagement.API.Common.Enums;
using ContractManagement.Common.Enums;
using ContractManagement.Domains.Policies.ContractTemplate;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Tests.Domains.Services.Contract;

public sealed partial class ContractServiceSlice04Tests
{
    [Theory]
    [InlineData(ContractType.SoftwareMaintenance, false)]
    [InlineData(ContractType.SoftwareUpkeep, false)]
    [InlineData(ContractType.SoftwareMaintenance, true)]
    [InlineData(ContractType.SoftwareUpkeep, true)]
    public async Task FollowUp_CreatesIndependentContractWithFrozenParentReference(ContractType type, bool bilingual)
    {
        await using var db = CreateContext();
        await SeedCreateDependenciesAsync(db);
        var service = CreateService(db);
        var parent = await service.CreateAsync(CreateRequest("VND", [CreateItem(ContractItemType.Product, 1, 100, sourceProductId: ProductId)]), EmployeeId);
        var original = await db.TblContracts.SingleAsync();
        original.Status = (byte)ContractStatus.Completed;
        original.SignDate = new DateTime(2026, 9, 1);
        var parentVersionId = original.CurrentVersionId!.Value;
        db.TblContractVersionLegalSnapshots.Add(new() {
            ContractId = parent.ContractId, VersionId = parentVersionId, VersionNo = 1,
            ContractCode = "HD-PARENT-SIGNED", ContractName = "Cung cấp phần mềm gốc",
            ContractNameEn = "Original supply contract", ContractType = 1, ContractDirection = 1,
            CurrencyCode = "VND", LanguageMode = 1, CreatedByEmployeeId = EmployeeId, CreatedDate = DateTime.UtcNow
        });
        (await db.TblContractTemplates.SingleAsync()).DocumentType = type == ContractType.SoftwareMaintenance
            ? (byte)TemplateDocumentType.SoftwareMaintenanceContract : (byte)TemplateDocumentType.SoftwareUpkeepContract;
        db.TblContractTemplateFields.Add(new() { TemplateVersionId = TemplateVersionId,
            PlaceholderKey = "CONTRACT_LEGAL_BASES", FieldLabel = "Căn cứ", DataSource = "Contract.LegalBases",
            SourceFieldKey = "system.CONTRACT_LEGAL_BASES", IsSystem = true,
            DataKind = (byte)TemplatePlaceholderDataKind.DynamicBlock,
            Multiplicity = (byte)TemplatePlaceholderMultiplicity.ZeroOrOne });
        if (bilingual)
        {
            (await db.TblContractTemplates.SingleAsync()).LanguageMode = (byte)ContractLanguageMode.Bilingual;
            var templateBasis = await db.TblContractTemplateLegalBases.SingleAsync();
            templateBasis.ContentEn = ContractManagement.Tests.ContractRichTextTestData.RichText("Pursuant to civil law.");
            var term = await db.TblContractTemplateTerms.SingleAsync();
            term.TermTitleEn = "General";
            term.TermContent = ContractManagement.Tests.ContractRichTextTestData.RichText("Điều khoản");
            term.TermContentEn = ContractManagement.Tests.ContractRichTextTestData.RichText("Terms");
        }
        await db.SaveChangesAsync();
        var request = CreateRequest("VND", [CreateItem(ContractItemType.Product, 1, 100, sourceProductId: ProductId)]);
        request.ContractType = type;
        request.ParentContractId = parent.ContractId;
        request.LanguageMode = bilingual ? ContractLanguageMode.Bilingual : ContractLanguageMode.Vietnamese;
        request.ContractNameEn = bilingual ? "Follow-up agreement" : null;
        var child = await service.CreateAsync(request, EmployeeId);
        var basis = await db.TblContractLegalBases.SingleAsync(x => x.ContractId == child.ContractId && x.BasisCode == "PARENT_CONTRACT");
        Assert.True(ContractManagement.Domains.Policies.ContractTemplate.ContractTermRichText.TryParse(basis.ContentVi, out var rich));
        var text = string.Concat(rich.Blocks.SelectMany(x => x.Runs).Select(x => x.Text));
        Assert.Contains("HD-PARENT-SIGNED", text);
        Assert.Contains("01/09/2026", text);
        Assert.Contains("Cung cấp phần mềm gốc", text);
        if (bilingual)
        {
            Assert.True(ContractTermRichText.TryParse(basis.ContentEn, out var english));
            var en = string.Concat(english.Blocks.SelectMany(x => x.Runs).Select(x => x.Text));
            Assert.Contains("HD-PARENT-SIGNED", en);
            Assert.Contains("Original supply contract", en);
            Assert.Contains("01 September 2026", en);
        }
        else Assert.Null(basis.ContentEn);
        Assert.Equal(parent.ContractId, (await db.TblContracts.SingleAsync(x => x.ContractId == child.ContractId)).ParentContractId);
        Assert.Equal((byte)ContractStatus.Completed, original.Status);
        Assert.DoesNotContain(await db.TblContractLegalBases.Where(x => x.ContractId == parent.ContractId).ToListAsync(), x => x.BasisCode == "PARENT_CONTRACT");
    }
}
