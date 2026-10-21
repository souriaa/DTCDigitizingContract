using ContractManagement.API.Common.Enums;
using ContractManagement.Domains.Policies.ContractTemplate;
using ContractManagement.Domains.Interfaces.ContractTemplate;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Domains.Services.ContractTemplate;

public sealed partial class ContractTemplateService
{
    private async Task ValidateTemplateTermLanguageAsync(int versionId, byte mode,
        string? titleEn, string? content, string? contentEn, CancellationToken ct)
    {
        var version = await GetVersionForMutationAsync(versionId, ct);
        var template = await GetTemplateAsync(version.TemplateId, ct);
        BilingualContentPolicy.Validate(mode, (ContractLanguageMode)template.LanguageMode,
            titleEn, content, contentEn);
    }

    private async Task ValidateTemplateBilingualContentsAsync(int versionId,
        ContractLanguageMode language, CancellationToken ct)
    {
        var terms = await _dbContext.TblContractTemplateTerms.AsNoTracking()
            .Where(x => x.TemplateVersionId == versionId).ToListAsync(ct);
        try
        {
            foreach (var term in terms)
                BilingualContentPolicy.Validate(term.BilingualContentMode, language,
                    term.TermTitleEn, term.TermContent, term.TermContentEn,
                    $"Điều khoản '{term.TermTitle}' (mã {term.TermCode})");
            if (language != ContractLanguageMode.Bilingual) return;
            var bases = await _dbContext.TblContractTemplateLegalBases.AsNoTracking()
                .Where(x => x.TemplateVersionId == versionId).ToListAsync(ct);
            foreach (var basis in bases)
                if (BilingualContentPolicy.HasContent(basis.ContentVi, $"Căn cứ '{basis.BasisCode}' — nội dung tiếng Việt")
                    && !BilingualContentPolicy.HasContent(basis.ContentEn, $"Căn cứ '{basis.BasisCode}' — nội dung tiếng Anh"))
                    throw new ArgumentException($"Căn cứ '{basis.BasisCode}' thiếu nội dung tiếng Anh.");
            var ids = terms.Select(x => x.TemplateTermId).ToArray();
            var milestones = await _dbContext.TblContractTemplatePaymentMilestones.AsNoTracking()
                .Where(x => ids.Contains(x.TemplateTermId)).ToListAsync(ct);
            foreach (var row in milestones)
            {
                if (string.IsNullOrWhiteSpace(row.TitleEn))
                    throw new ArgumentException($"Đợt thanh toán '{row.TitleVi}' (mã {row.MilestoneCode}): thiếu tên tiếng Anh.");
                if (!string.IsNullOrWhiteSpace(row.ConditionVi) && string.IsNullOrWhiteSpace(row.ConditionEn))
                    throw new ArgumentException($"Đợt thanh toán '{row.TitleVi}' (mã {row.MilestoneCode}): thiếu điều kiện thanh toán tiếng Anh.");
            }
        }
        catch (ArgumentException ex)
        {
            throw new ContractTemplatePreviewException("BilingualContentInvalid", ex.Message);
        }
    }
}
