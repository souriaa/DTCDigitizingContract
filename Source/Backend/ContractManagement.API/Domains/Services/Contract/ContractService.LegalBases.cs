using ContractManagement.API.Common.Enums;
using ContractManagement.API.Domains.DTOs.Requests.Contract;
using ContractManagement.API.Domains.DTOs.Responses.Contract;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Domains.Services.Contract;

public partial class ContractService
{
    private async Task<List<ContractLegalBasisResponse>> LoadLegalBasesAsync(int contractId, int versionId)
    {
        var rows = await _dbContext.TblContractLegalBases.AsNoTracking()
            .Where(x => x.ContractId == contractId && x.VersionId == versionId)
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.LegalBasisId).ToListAsync();
        return rows.Select(x => new ContractLegalBasisResponse {
            LegalBasisId = x.LegalBasisId, SourceTemplateLegalBasisId = x.SourceTemplateLegalBasisId,
            BasisCode = x.BasisCode, ContentVi = x.ContentVi, ContentEn = x.ContentEn,
            DisplayOrder = x.DisplayOrder, RowVersion = EncodeRowVersion(x.RowVersion)
        }).ToList();
    }

    private async Task<List<string>> UpdateLegalBasesAsync(TblContract contract, TblContractVersion version,
        List<UpdateContractLegalBasisRequest> requested, int employeeId, DateTime now)
    {
        if (requested is null || requested.Any(x => x is null || x.LegalBasisId <= 0) ||
            requested.Select(x => x.LegalBasisId).Distinct().Count() != requested.Count)
            throw new ArgumentException("Danh sách căn cứ cần sửa không hợp lệ hoặc bị trùng.");
        if (requested.Count == 0) return [];
        var ids = requested.Select(x => x.LegalBasisId).ToList();
        var bases = await _dbContext.TblContractLegalBases
            .Where(x => x.ContractId == contract.ContractId && x.VersionId == version.VersionId && ids.Contains(x.LegalBasisId))
            .ToDictionaryAsync(x => x.LegalBasisId);
        if (bases.Count != requested.Count)
            throw new ArgumentException("Căn cứ không thuộc phiên bản hợp đồng hiện tại.");
        var changed = new List<string>();
        foreach (var request in requested)
        {
            var basis = bases[request.LegalBasisId];
            var expected = DecodeRowVersion(request.RowVersion, "LegalBases.RowVersion");
            EnsureRowVersionMatches(basis.RowVersion, expected, "Căn cứ hợp đồng");
            var vi = NormalizeRichText(request.ContentVi);
            var en = NormalizeRichText(request.ContentEn);
            if (string.IsNullOrWhiteSpace(vi))
                throw new ArgumentException($"Căn cứ '{basis.BasisCode}' cần nội dung tiếng Việt.");
            if (contract.LanguageMode == (byte)ContractLanguageMode.Bilingual && string.IsNullOrWhiteSpace(en))
                throw new ArgumentException($"Căn cứ '{basis.BasisCode}' cần nội dung tiếng Anh.");
            if (basis.ContentVi == vi && basis.ContentEn == en) continue;
            _dbContext.Entry(basis).Property(x => x.RowVersion).OriginalValue = expected;
            basis.ContentVi = vi;
            basis.ContentEn = en;
            basis.UpdatedEmployeeId = employeeId;
            basis.UpdatedDate = now;
            changed.Add(basis.BasisCode);
        }
        return changed;
    }
}
