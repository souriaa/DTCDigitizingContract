using ContractManagement.API.Common.Enums;
using ContractManagement.API.Domains.DTOs.Requests.Contract;
using ContractManagement.API.Domains.DTOs.Responses.Contract;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Domains.Services.Contract;

public partial class ContractService
{
    private async Task AddParentContractLegalBasisAsync(TblContract contract, TblContractVersion version,
        List<TblContractLegalBasis> bases, int employeeId, DateTime now)
    {
        if (contract.ContractType == (byte)ContractType.SoftwareSupply) return;
        var parentId = contract.ParentContractId
            ?? throw new InvalidOperationException("Hợp đồng duy trì/bảo trì phải có hợp đồng gốc.");
        var parent = await _dbContext.TblContracts.AsNoTracking().SingleAsync(x => x.ContractId == parentId);
        if (!parent.CurrentVersionId.HasValue)
            throw new InvalidOperationException("Hợp đồng gốc chưa có phiên bản đã ký.");
        var signed = await _dbContext.TblContractVersionLegalSnapshots.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ContractId == parentId && x.VersionId == parent.CurrentVersionId.Value)
            ?? throw new InvalidOperationException("Hợp đồng gốc thiếu dữ liệu pháp lý đã khóa.");
        // The legal snapshot is frozen before signing; the signing workflow
        // records the actual business sign date on Contract, not that snapshot.
        if (string.IsNullOrWhiteSpace(signed.ContractCode) || !parent.SignDate.HasValue)
            throw new InvalidOperationException("Hợp đồng gốc thiếu mã hoặc ngày ký.");
        if (contract.LanguageMode == (byte)ContractLanguageMode.Bilingual && string.IsNullOrWhiteSpace(signed.ContractNameEn))
            throw new InvalidOperationException("Hợp đồng gốc thiếu tên tiếng Anh để tạo căn cứ song ngữ.");
        const string code = "PARENT_CONTRACT";
        if (bases.Any(x => x.BasisCode.Equals(code, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Mã căn cứ PARENT_CONTRACT được dành cho hợp đồng gốc.");
        var vi = $"Căn cứ Hợp đồng số {signed.ContractCode} ký ngày {parent.SignDate.Value:dd/MM/yyyy} về việc {signed.ContractName}.";
        var en = $"Pursuant to Contract No. {signed.ContractCode} signed on {parent.SignDate.Value.ToString("dd MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture)} regarding {signed.ContractNameEn}.";
        string Rich(string text) => ContractManagement.Domains.Policies.ContractTemplate.ContractTermRichText.Prefix
            + System.Text.Json.JsonSerializer.Serialize(new ContractManagement.Domains.Policies.ContractTemplate.ContractTermRichTextDocument {
                Blocks = [new ContractManagement.Domains.Policies.ContractTemplate.ContractTermRichTextBlock {
                    Type = "paragraph", Runs = [new ContractManagement.Domains.Policies.ContractTemplate.ContractTermRichTextRun { Text = text }]
                }]
            });
        bases.Add(new TblContractLegalBasis {
            ContractId = contract.ContractId, VersionId = version.VersionId,
            BasisCode = code, ContentVi = Rich(vi),
            ContentEn = contract.LanguageMode == (byte)ContractLanguageMode.Bilingual ? Rich(en) : null,
            DisplayOrder = bases.Count == 0 ? 0 : bases.Max(x => x.DisplayOrder) + 1,
            CreatedEmployeeId = employeeId, CreatedDate = now
        });
    }

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
