using ContractManagement.API.Common.Enums;
using ContractManagement.API.Domains.DTOs.Requests.Contract;
using ContractManagement.API.Domains.DTOs.Responses.Contract;
using ContractManagement.Common.Enums;
using ContractManagement.Domains.Interfaces.Contract;
using ContractManagement.Domains.Policies.Contract;
using ContractManagement.Domains.Policies.ContractTemplate;
using ContractManagement.Domains.Services.ContractTemplate;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;
namespace ContractManagement.Domains.Services.Contract;
public sealed partial class ContractHandoverService
{
    public async Task<HandoverReadinessResponse> ConfirmScopeAsync(int contractId, ConfirmHandoverScopeRequest request, int employeeId, CancellationToken ct = default)
    {
        await authorization.EnsureCanManageAcceptanceAsync(contractId, employeeId, ct);
        return await Execute(contractId, async () => {
            var source = await SourceAsync(contractId, request.ContractVersionId, true, ct);
            Match(source.Contract.RowVersion, request.ContractRowVersion); Match(source.Version.RowVersion, request.VersionRowVersion);
            if (await db.TblContractHandoverBaselines.AnyAsync(x => x.ContractId == contractId && x.ContractVersionId == request.ContractVersionId, ct))
                throw Rule("HandoverScopeAlreadyConfirmed", "Phạm vi đã xác nhận và bất biến theo phiên bản hợp đồng.");
            var items = await db.TblContractItems.AsNoTracking().Where(x => x.ContractId == contractId && x.VersionId == request.ContractVersionId)
                .OrderBy(x => x.DisplayOrder).ThenBy(x => x.ContractItemId).ToListAsync(ct);
            if (items.Count == 0 || request.Items.Count != items.Count || request.Items.Select(x => x.ContractItemId).Distinct().Count() != items.Count
                || !request.Items.Select(x => x.ContractItemId).ToHashSet().SetEquals(items.Select(x => x.ContractItemId)))
                throw Rule("HandoverScopeIncomplete", "Phải xác nhận toàn bộ hạng mục đã ký; không được thêm/bớt nghĩa vụ hoặc dùng hạng mục từ nguồn khác.");
            var scopeItems = items.Select((item, index) => {
                var mode = request.Items.Single(x => x.ContractItemId == item.ContractItemId).Mode;
                HandoverCoveragePolicy.Quantity(item.Quantity, HandoverScopeMode.Quantity);
                if (!Enum.IsDefined(mode)) throw Rule("HandoverScopeModeRequired", "Chọn cách theo dõi theo số lượng hoặc nguyên gói cho từng hạng mục.");
                if ((ContractLanguageMode)source.Legal.LanguageMode == ContractLanguageMode.Bilingual)
                    _ = Text(item.ItemNameEn, 2000, "Tên hạng mục tiếng Anh trong hợp đồng đã ký");
                return new TblContractHandoverScopeItem {
                    SourceContractItemId = item.ContractItemId, ScopeMode = (byte)mode, ItemCode = $"SCOPE-{index + 1}",
                    NameVi = Text(item.ItemName + (string.IsNullOrWhiteSpace(item.ItemDescription) ? "" : " — " + item.ItemDescription), 2000, "Hạng mục"),
                    NameEn = Optional(item.ItemNameEn + (string.IsNullOrWhiteSpace(item.ItemDescriptionEn) ? "" : " — " + item.ItemDescriptionEn), 2000),
                    UnitVi = mode == HandoverScopeMode.WholePackage ? "Gói" : Text(item.UnitName, 100, "Đơn vị đã ký (hoặc chọn nguyên gói)"),
                    UnitEn = mode == HandoverScopeMode.WholePackage ? "Package" : Optional(item.UnitNameEn, 100),
                    SourceQuantity = item.Quantity, RequiredQuantity = mode == HandoverScopeMode.WholePackage ? 1 : item.Quantity, DisplayOrder = index + 1
                };
            }).ToList();
            if ((ContractLanguageMode)source.Legal.LanguageMode == ContractLanguageMode.Bilingual && scopeItems.Any(x => string.IsNullOrWhiteSpace(x.UnitEn)))
                throw Rule("HandoverScopeEnglishMissing", "Đơn vị hạng mục song ngữ phải có tiếng Anh trong dữ liệu đã ký; không tự dịch.");
            var baseline = new TblContractHandoverBaseline { ContractId = contractId, ContractVersionId = source.Version.VersionId, ConfirmedByEmployeeId = employeeId, ConfirmedAt = DateTime.UtcNow };
            db.TblContractHandoverBaselines.Add(baseline); await db.SaveChangesAsync(ct);
            foreach (var item in scopeItems) item.HandoverBaselineId = baseline.HandoverBaselineId;
            db.TblContractHandoverScopeItems.AddRange(scopeItems);
            audit.StageEmployeeAudits([new(contractId, source.Version.VersionId, employeeId, ContractAuditActionTypes.HandoverScopeConfirmed, ContractAuditResults.Succeeded,
                DateTime.UtcNow, SubjectType: ContractAuditSubjectTypes.ContractVersion, SubjectId: source.Version.VersionId,
                NewValues: ContractAuditValues.Create(("HandoverBaselineId", baseline.HandoverBaselineId), ("HandoverLineCount", scopeItems.Count), ("CurrentVersionId", source.Version.VersionId)))]);
            await db.SaveChangesAsync(ct);
            return await ReadinessAsync(contractId, employeeId, ct);
        }, ct);
    }
    public async Task<HandoverDetailResponse> CreateAsync(int contractId, CreateContractHandoverRequest request, int employeeId, CancellationToken ct = default)
    {
        await authorization.EnsureCanManageAcceptanceAsync(contractId, employeeId, ct);
        return await Execute(contractId, async () => {
            var source = await SourceAsync(contractId, request.ContractVersionId, true, ct);
            Match(source.Contract.RowVersion, request.ContractRowVersion); Match(source.Version.RowVersion, request.VersionRowVersion);
            var progress = await ReadinessAsync(contractId, employeeId, ct);
            if (!progress.CanCreate) throw Rule(progress.BlockerCode!, progress.BlockerMessage!);
            var template = await TemplateAsync(source, request.TemplateVersionId, true, ct);
            var code = await CodeAsync(contractId, request.HandoverCode, null, ct);
            ValidateMetadata(request, source);
            await ValidateReferencesAsync(source, request.AppendixIds, ct);
            await ValidateLinesAsync(source, progress.BaselineId!.Value, request.Lines, null, ct);
            var sections = await db.TblContractTemplateTerms.AsNoTracking().Where(x => x.TemplateVersionId == request.TemplateVersionId)
                .OrderBy(x => x.DisplayOrder).ThenBy(x => x.TemplateTermId).ToListAsync(ct);
            if (sections.Any(x => x.TermKind != (byte)ContractTermKind.General)) throw Rule("HandoverSectionsInvalid", "Mẫu bàn giao không được có điều khoản thanh toán.");
            var sectionInputs = sections.Select(x => new HandoverSectionInput { SectionCode = x.TermCode, TitleVi = x.TermTitle, TitleEn = x.TermTitleEn,
                ContentVi = x.TermContent, ContentEn = x.TermContentEn, BilingualContentMode = x.BilingualContentMode }).ToArray();
            await ValidateSectionsAsync(request.TemplateVersionId, sectionInputs, (ContractLanguageMode)source.Legal.LanguageMode, ct);
            var parties = source.Parties.Select(x => new TblContractHandoverParty {
                SubjectKind = x.PartyRole, LegalName = x.LegalName, LegalNameEn = x.LegalNameEn, Address = x.Address, AddressEn = x.AddressEn,
                Phone = x.PhoneNumber, Fax = x.FaxNumber, TaxCode = x.TaxCode,
                RepresentativeName = Text(x.PartyRole == 1 ? x.RepresentativeName : x.ContactName, 300, "Người giao/nhận trong snapshot"),
                RepresentativeTitle = x.PartyRole == 1 ? x.RepresentativeTitle : x.ContactTitle,
                RepresentativeTitleEn = x.PartyRole == 1 ? x.RepresentativeTitleEn : x.ContactTitleEn }).ToList();
            var row = new TblContractHandoverRecord { ContractId = contractId, ContractVersionId = source.Version.VersionId,
                HandoverBaselineId = progress.BaselineId.Value, TemplateVersionId = template.Version.TemplateVersionId,
                HandoverCode = code, HandoverDate = request.HandoverDate.Date, ContractSignDate = source.Contract.SignDate!.Value.Date,
                Location = Text(request.Location, 500, "Địa điểm"), LocationEn = Optional(request.LocationEn, 500),
                Status = (byte)HandoverRecordStatus.Draft, CreatedEmployeeId = employeeId, CreatedAt = DateTime.UtcNow,
                RowVersion = Memory ? Guid.NewGuid().ToByteArray() : [] };
            db.TblContractHandoverRecords.Add(row); await db.SaveChangesAsync(ct);
            foreach (var party in parties) party.HandoverRecordId = row.HandoverRecordId;
            db.TblContractHandoverParties.AddRange(parties);
            AddChildren(row.HandoverRecordId, request.Lines, request.AppendixIds, sectionInputs);
            StageAudit(row, employeeId, ContractAuditActionTypes.HandoverCreated, request.Lines.Count);
            await StageNotificationAsync(row, employeeId, "HandoverCreated", "Đã tạo bản nháp biên bản bàn giao", ct);
            await db.SaveChangesAsync(ct); return await GetAsync(contractId, row.HandoverRecordId, employeeId, ct);
        }, ct);
    }
    public async Task<HandoverDetailResponse> UpdateAsync(int contractId, int recordId, UpdateContractHandoverRequest request, int employeeId, CancellationToken ct = default)
    {
        await authorization.EnsureCanManageAcceptanceAsync(contractId, employeeId, ct);
        return await Execute(contractId, async () => {
            var row = await DraftAsync(contractId, recordId, request.RowVersion, ct);
            var source = await SourceAsync(contractId, row.ContractVersionId, true, ct);
            _ = await TemplateAsync(source, row.TemplateVersionId, false, ct);
            var code = await CodeAsync(contractId, request.HandoverCode, recordId, ct);
            ValidateMetadata(request, source);
            await ValidateSectionsAsync(row.TemplateVersionId, request.Sections, (ContractLanguageMode)source.Legal.LanguageMode, ct);
            await ValidateReferencesAsync(source, request.AppendixIds, ct);
            await ValidateLinesAsync(source, row.HandoverBaselineId, request.Lines, recordId, ct);
            var parties = await db.TblContractHandoverParties.Where(x => x.HandoverRecordId == recordId).ToListAsync(ct);
            if (parties.Count != 2 || request.Parties.Count != 2 || request.Parties.Select(x => x.SubjectKind).Distinct().Count() != 2)
                throw Rule("HandoverPartiesInvalid", "Phải có đúng hai chủ thể Doanh nghiệp/Đối tác.");
            foreach (var input in request.Parties)
            {
                var target = parties.SingleOrDefault(x => x.SubjectKind == input.SubjectKind) ?? throw Rule("HandoverPartiesInvalid", "Chủ thể không hợp lệ.");
                if (Text(input.LegalName, 1000, "Tên pháp nhân") != target.LegalName || Optional(input.LegalNameEn, 1000) != target.LegalNameEn)
                    throw Rule("HandoverLegalNameImmutable", "Tên pháp nhân lấy từ snapshot, không được sửa.");
                target.Address = Optional(input.Address, 2000); target.AddressEn = Optional(input.AddressEn, 2000);
                target.Phone = Optional(input.Phone, 100); target.Fax = Optional(input.Fax, 100); target.TaxCode = Optional(input.TaxCode, 50);
                target.RepresentativeName = Text(input.RepresentativeName, 300, "Người giao/nhận");
                target.RepresentativeTitle = Optional(input.RepresentativeTitle, 500); target.RepresentativeTitleEn = Optional(input.RepresentativeTitleEn, 500);
            }
            db.TblContractHandoverLines.RemoveRange(await db.TblContractHandoverLines.Where(x => x.HandoverRecordId == recordId).ToListAsync(ct));
            db.TblContractHandoverReferences.RemoveRange(await db.TblContractHandoverReferences.Where(x => x.HandoverRecordId == recordId).ToListAsync(ct));
            db.TblContractHandoverSections.RemoveRange(await db.TblContractHandoverSections.Where(x => x.HandoverRecordId == recordId).ToListAsync(ct));
            // Flush deletes before inserting swapped section orders / references under unique indexes.
            await db.SaveChangesAsync(ct);
            AddChildren(recordId, request.Lines, request.AppendixIds, request.Sections);
            row.HandoverCode = code; row.HandoverDate = request.HandoverDate.Date; row.Location = Text(request.Location, 500, "Địa điểm");
            row.LocationEn = Optional(request.LocationEn, 500); Touch(row, employeeId);
            StageAudit(row, employeeId, ContractAuditActionTypes.HandoverUpdated, request.Lines.Count);
            await db.SaveChangesAsync(ct); return await GetAsync(contractId, recordId, employeeId, ct);
        }, ct);
    }
    private void AddChildren(int id, IReadOnlyList<HandoverLineInput> lines, IReadOnlyList<int> appendixIds, IReadOnlyList<HandoverSectionInput> sections)
    {
        db.TblContractHandoverLines.AddRange(lines.Select((x, i) => new TblContractHandoverLine {
            HandoverRecordId = id, HandoverScopeItemId = x.HandoverScopeItemId, Quantity = x.Quantity,
            DisplayOrder = i + 1, NotesVi = Optional(x.NotesVi, 2000), NotesEn = Optional(x.NotesEn, 2000) }));
        db.TblContractHandoverReferences.Add(new() { HandoverRecordId = id, DisplayOrder = 0 });
        db.TblContractHandoverReferences.AddRange(appendixIds.Select((x, i) => new TblContractHandoverReference { HandoverRecordId = id, AppendixId = x, DisplayOrder = i + 1 }));
        db.TblContractHandoverSections.AddRange(sections.Select((x, i) => new TblContractHandoverSection {
            HandoverRecordId = id, SectionCode = x.SectionCode.Trim().ToUpperInvariant(), TitleVi = x.TitleVi.Trim(), TitleEn = Optional(x.TitleEn, 1000),
            ContentVi = RichText(x.ContentVi), ContentEn = RichText(x.ContentEn), BilingualContentMode = x.BilingualContentMode, DisplayOrder = i + 1 }));
    }
    private static string? RichText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!ContractTermRichText.TryParse(value.Trim(), out _)) throw Rule("HandoverRichTextInvalid", "Nội dung không đúng rich text schema hiện hành.");
        return value.Trim();
    }
    private async Task ValidateSectionsAsync(int templateVersionId, IReadOnlyList<HandoverSectionInput> sections, ContractLanguageMode language, CancellationToken ct)
    {
        if (sections.Count == 0 && await SupportingDocumentSectionReader.IsRequiredAsync(
                db, templateVersionId, TemplateDocumentType.HandoverRecord, ct))
            throw Rule("HandoverSectionsMissing", "DOCX có {{HANDOVER_SECTIONS}} nên cần ít nhất một mục bàn giao.");
        if (sections.Count > 100 || sections.Select(x => x.SectionCode.Trim().ToUpperInvariant()).Distinct().Count() != sections.Count)
            throw Rule("HandoverSectionsInvalid", "Tối đa 100 mục nội dung và mã không được trùng.");
        foreach (var x in sections) {
            _ = Text(x.SectionCode, 100, "Mã mục"); _ = Text(x.TitleVi, 1000, "Tiêu đề");
            _ = RichText(x.ContentVi); _ = RichText(x.ContentEn);
            BilingualContentPolicy.Validate(x.BilingualContentMode, language, x.TitleEn, x.ContentVi, x.ContentEn, x.SectionCode);
        }
    }
    private static void ValidateMetadata(HandoverMetadataRequest request, Source source)
    {
        _ = Text(request.HandoverCode, 100, "Mã biên bản"); _ = Text(request.Location, 500, "Địa điểm");
        if (request.HandoverDate == default || request.HandoverDate.Date < source.Contract.SignDate!.Value.Date)
            throw Rule("HandoverDateInvalid", "Ngày bàn giao không được trước ngày ký hợp đồng.");
        if ((ContractLanguageMode)source.Legal.LanguageMode == ContractLanguageMode.Bilingual && string.IsNullOrWhiteSpace(request.LocationEn))
            throw Rule("HandoverLocationEnRequired", "Địa điểm tiếng Anh là bắt buộc với hợp đồng song ngữ.");
        _ = Optional(request.LocationEn, 500);
    }
    private async Task<string> CodeAsync(int contractId, string value, int? id, CancellationToken ct)
    {
        var code = Text(value, 100, "Mã biên bản").ToUpperInvariant();
        if (await db.TblContractHandoverRecords.AnyAsync(x => x.ContractId == contractId && x.HandoverCode == code && x.HandoverRecordId != id, ct))
            throw Rule("HandoverCodeAlreadyExists", "Mã biên bản bàn giao đã tồn tại, kể cả bản đã hủy. Vui lòng chọn mã khác.");
        return code;
    }
    private async Task ValidateReferencesAsync(Source source, IReadOnlyList<int> ids, CancellationToken ct)
    {
        if (ids.Count > 100 || ids.Distinct().Count() != ids.Count ||
            await db.TblContractAppendices.CountAsync(x => x.ContractId == source.Contract.ContractId && x.VersionId == source.Version.VersionId && ids.Contains(x.AppendixId), ct) != ids.Count)
            throw Rule("HandoverReferenceMismatch", "Phụ lục phải thuộc đúng phiên bản đã ký và không được trùng.");
    }
    private async Task ValidateLinesAsync(Source source, long baselineId, IReadOnlyList<HandoverLineInput> inputs, int? recordId, CancellationToken ct)
    {
        if (inputs.Count == 0 || inputs.Count > 1000 || inputs.Select(x => x.HandoverScopeItemId).Distinct().Count() != inputs.Count)
            throw Rule("HandoverLinesInvalid", "Cần hạng mục bàn giao, không trùng dòng.");
        var baseline = await db.TblContractHandoverBaselines.AsNoTracking().SingleOrDefaultAsync(x => x.HandoverBaselineId == baselineId && x.ContractId == source.Contract.ContractId && x.ContractVersionId == source.Version.VersionId, ct)
            ?? throw Rule("HandoverScopeMismatch", "Phạm vi không thuộc phiên bản hợp đồng.");
        var scope = await db.TblContractHandoverScopeItems.AsNoTracking().Where(x => x.HandoverBaselineId == baseline.HandoverBaselineId).ToListAsync(ct);
        var reserved = await (from l in db.TblContractHandoverLines.AsNoTracking()
            join r in db.TblContractHandoverRecords.AsNoTracking() on l.HandoverRecordId equals r.HandoverRecordId
            where r.ContractId == source.Contract.ContractId && r.ContractVersionId == source.Version.VersionId &&
                r.HandoverRecordId != recordId && (r.Status == 1 || r.Status == 2)
            select new { l.HandoverScopeItemId, l.Quantity }).ToListAsync(ct);
        foreach (var line in inputs)
        {
            var item = scope.SingleOrDefault(x => x.HandoverScopeItemId == line.HandoverScopeItemId)
                ?? throw Rule("HandoverScopeMismatch", "Hạng mục không thuộc phạm vi phiên bản đã ký.");
            HandoverCoveragePolicy.Quantity(line.Quantity, (HandoverScopeMode)item.ScopeMode);
            if (line.Quantity + reserved.Where(x => x.HandoverScopeItemId == item.HandoverScopeItemId).Sum(x => x.Quantity) > item.RequiredQuantity)
                throw Rule("HandoverQuantityExceeded", $"Hạng mục {item.ItemCode} vượt lượng còn có thể bàn giao.");
            _ = Optional(line.NotesVi, 2000); _ = Optional(line.NotesEn, 2000);
        }
    }
}
