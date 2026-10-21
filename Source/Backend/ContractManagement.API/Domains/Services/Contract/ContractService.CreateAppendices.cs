using ContractManagement.API.Domains.DTOs.Requests.Contract;
using ContractManagement.API.Common.Enums;
using ContractManagement.Domains.Policies.ContractTemplate;
using ContractManagement.Infrastructure.Persistence.Application.Models;

namespace ContractManagement.Domains.Services.Contract;

public partial class ContractService
{
    private static List<CreateContractAppendixDraftRequest> BuildCreateAppendices(
        CreateContractRequest request,
        IReadOnlyList<TblContractTemplateAppendix> sources,
        IReadOnlyList<TblContractTemplateAppendixTerm> sourceTerms)
    {
        if (request.Appendices is not null && request.SelectedOptionalTemplateAppendixIds.Count > 0)
            throw new ArgumentException("Chỉ gửi danh sách phụ lục đã soạn hoặc danh sách chọn từ mẫu, không gửi đồng thời.");

        var drafts = request.Appendices ?? sources
            .Where(source => source.IsRequired || request.SelectedOptionalTemplateAppendixIds.Contains(source.TemplateAppendixId))
            .Select(source => new CreateContractAppendixDraftRequest
            {
                SourceTemplateAppendixId = source.TemplateAppendixId,
                AppendixCode = source.AppendixCode,
                AppendixName = source.AppendixName,
                AppendixNameEn = source.AppendixNameEn,
                AppendixDescription = source.AppendixDescription,
                Terms = sourceTerms.Where(term => term.TemplateAppendixId == source.TemplateAppendixId)
                    .Select(term => new CreateContractAppendixTermDraftRequest
                    {
                        SourceTemplateAppendixTermId = term.TemplateAppendixTermId,
                        TermCode = term.TermCode, TermTitle = term.TermTitle, TermTitleEn = term.TermTitleEn,
                        TermContent = term.TermContent, TermContentEn = term.TermContentEn,
                        BilingualContentMode = term.BilingualContentMode
                    }).ToList()
            }).ToList();

        var sourceIds = new HashSet<int>();
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var draft in drafts)
        {
            if (draft is null) throw new ArgumentException("Phụ lục không hợp lệ.");
            if (draft.SourceTemplateAppendixId is int sourceId)
            {
                if (!sources.Any(source => source.TemplateAppendixId == sourceId))
                    throw new ArgumentException("Phụ lục được chọn không thuộc phiên bản mẫu của hợp đồng.");
                if (!sourceIds.Add(sourceId)) throw new ArgumentException("Không được thêm cùng một phụ lục mẫu nhiều lần.");
            }
            draft.AppendixCode = NormalizeAppendixValue(draft.AppendixCode, 50, "Mã phụ lục").ToUpperInvariant();
            draft.AppendixName = NormalizeAppendixValue(draft.AppendixName, 500, "Tên phụ lục");
            draft.AppendixNameEn = NormalizeAppendixOptional(draft.AppendixNameEn, 500, "Tên phụ lục tiếng Anh");
            draft.AppendixDescription = NormalizeAppendixOptional(draft.AppendixDescription, 2000, "Mô tả phụ lục");
            if (!codes.Add(draft.AppendixCode)) throw new ArgumentException($"Mã phụ lục '{draft.AppendixCode}' bị trùng.");
            if (request.LanguageMode == ContractLanguageMode.Bilingual && draft.AppendixNameEn is null)
                throw new ArgumentException($"Phụ lục '{draft.AppendixCode}' phải có tên tiếng Anh.");
            if (draft.Terms is null || draft.Terms.Count == 0)
                throw new ArgumentException($"Phụ lục '{draft.AppendixCode}' phải có ít nhất một điều khoản.");
            var termCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var termSourceIds = new HashSet<int>();
            foreach (var term in draft.Terms)
            {
                if (term is null) throw new ArgumentException("Điều khoản phụ lục không hợp lệ.");
                if (term.SourceTemplateAppendixTermId is int termSourceId)
                {
                    if (!sourceTerms.Any(source => source.TemplateAppendixTermId == termSourceId
                        && source.TemplateAppendixId == draft.SourceTemplateAppendixId))
                        throw new ArgumentException($"Điều khoản nguồn không thuộc phụ lục '{draft.AppendixCode}'.");
                    if (!termSourceIds.Add(termSourceId)) throw new ArgumentException("Điều khoản nguồn bị trùng trong phụ lục.");
                }
                term.TermCode = NormalizeAppendixValue(term.TermCode, 100, "Mã điều khoản").ToUpperInvariant();
                term.TermTitle = NormalizeAppendixValue(term.TermTitle, 1000, "Tiêu đề điều khoản");
                term.TermTitleEn = NormalizeAppendixOptional(term.TermTitleEn, 1000, "Tiêu đề tiếng Anh");
                term.TermContent = NormalizeOptional(term.TermContent);
                term.TermContentEn = NormalizeOptional(term.TermContentEn);
                if (!termCodes.Add(term.TermCode)) throw new ArgumentException($"Phụ lục '{draft.AppendixCode}': mã điều khoản '{term.TermCode}' bị trùng.");
                BilingualContentPolicy.Validate(term.BilingualContentMode, request.LanguageMode,
                    term.TermTitleEn, term.TermContent, term.TermContentEn,
                    $"Phụ lục '{draft.AppendixCode}' — điều khoản '{term.TermCode}'");
            }
        }
        if (sources.Any(source => source.IsRequired && !sourceIds.Contains(source.TemplateAppendixId)))
            throw new ArgumentException("Không được rút phụ lục bắt buộc khỏi hợp đồng.");
        return drafts;
    }
}
