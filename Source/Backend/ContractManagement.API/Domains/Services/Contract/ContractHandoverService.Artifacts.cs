using System.Security.Cryptography;
using System.Text;
using ContractManagement.API.Common.Enums;
using ContractManagement.API.Domains.DTOs.Requests.Contract;
using ContractManagement.API.Domains.DTOs.Responses.Contract;
using ContractManagement.Domains.Interfaces.Contract;
using ContractManagement.Domains.Interfaces.File;
using ContractManagement.Domains.Services.File;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;
namespace ContractManagement.Domains.Services.Contract;
public sealed partial class ContractHandoverService
{
    private async Task<byte[]> ReadFileAsync(TblFileStorage file, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(file.StorageKey) || string.IsNullOrWhiteSpace(file.TenantCode) ||
            !string.Equals(file.TenantCode, tenant.GetRequiredTenant().TenantCode, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(file.Sha256))
            throw Rule("HandoverFileInvalid", "Tệp không thuộc private storage của doanh nghiệp hiện tại.");
        await using var stream = await files.OpenReadAsync(file.TenantCode, file.StorageKey, ct);
        using var memory = new MemoryStream(); await stream.CopyToAsync(memory, ct);
        var bytes = memory.ToArray();
        if (!string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), file.Sha256, StringComparison.OrdinalIgnoreCase))
            throw Rule("HandoverFileHashMismatch", "Tệp đã thay đổi hoặc không khớp hash đã lưu.");
        return bytes;
    }
    private static string SafeName(string code) => string.Concat(code.Select(x => Path.GetInvalidFileNameChars().Contains(x) || x is '/' or '\\' ? '-' : x));
    private async Task<StoredPrivateFile> SaveAsync(byte[] bytes, string name, string type, string objectType, int id, CancellationToken ct)
    {
        await using var stream = new MemoryStream(bytes, false);
        return await files.SaveAsync(new(stream, name, type, bytes.Length, tenant.GetRequiredTenant().TenantCode, objectType, id,
            type == DocxType ? PrivateFileUploadPolicies.SubmittedContractDocx() : PrivateFileUploadPolicies.SubmittedContractPdf()), ct);
    }
    private static TblFileStorage FileRow(StoredPrivateFile file, string objectType, int id, int employee) => new() {
        ObjectType = objectType, ObjectId = id, FileName = file.OriginalFileName, FilePath = "", StorageKey = file.StorageKey,
        TenantCode = file.TenantCode, ContentType = file.ContentType, FileSize = file.FileSize, Sha256 = file.Sha256,
        FileType = Path.GetExtension(file.OriginalFileName).TrimStart('.').ToLowerInvariant(), UploadedByUserId = employee, UploadedDate = file.CreatedAt
    };
    private static string Format(string value) => value.ToLowerInvariant() is "docx" or "pdf" ? value.ToLowerInvariant()
        : throw Rule("HandoverFormatInvalid", "Chỉ hỗ trợ DOCX hoặc PDF.");
    public async Task<ContractAcceptanceDocumentContent> PreviewAsync(int contractId, int recordId, string format, int employeeId, CancellationToken ct = default)
    {
        await authorization.EnsureCanManageAcceptanceAsync(contractId, employeeId, ct);
        var row = await RecordAsync(contractId, recordId, ct);
        if (row.Status != (byte)HandoverRecordStatus.Draft) throw Rule("HandoverNotDraft", "Biên bản đã chốt phải tải tệp đã lưu, không render lại.");
        var normalized = Format(format); var render = await RenderAsync(contractId, recordId, employeeId, ct);
        return new(normalized == "pdf" ? await pdfRenderer.ConvertPreviewToPdfAsync(render.Docx, ct) : render.Docx,
            SafeName(row.HandoverCode) + "-preview." + normalized, normalized == "pdf" ? "application/pdf" : DocxType);
    }
    public async Task<HandoverDetailResponse> FinalizeAsync(int contractId, int recordId, HandoverRowVersionRequest request, int employeeId, CancellationToken ct = default)
    {
        await authorization.EnsureCanManageAcceptanceAsync(contractId, employeeId, ct);
        var stored = new List<StoredPrivateFile>();
        return await Execute(contractId, async () => {
            var row = await DraftAsync(contractId, recordId, request.RowVersion, ct);
            var source = await SourceAsync(contractId, row.ContractVersionId, true, ct);
            var lines = await db.TblContractHandoverLines.AsNoTracking().Where(x => x.HandoverRecordId == recordId).OrderBy(x => x.DisplayOrder).ToListAsync(ct);
            await ValidateLinesAsync(source, row.HandoverBaselineId, lines.Select(x => new HandoverLineInput(x.HandoverScopeItemId, x.Quantity, x.NotesVi, x.NotesEn)).ToArray(), recordId, ct);
            var detail = await GetAsync(contractId, recordId, employeeId, ct);
            var rendered = await RenderAsync(contractId, recordId, employeeId, ct);
            var pdf = await pdfRenderer.ConvertPreviewToPdfAsync(rendered.Docx, ct);
            if (await db.TblFileStorages.AnyAsync(x => x.ObjectType == ArtifactType && x.ObjectId == recordId, ct))
                throw Rule("HandoverArtifactsExist", "Biên bản đã có tệp chốt, không được ghi đè.");
            stored.Add(await SaveAsync(rendered.Docx, SafeName(row.HandoverCode) + ".docx", DocxType, ArtifactType, recordId, ct));
            stored.Add(await SaveAsync(pdf, SafeName(row.HandoverCode) + ".pdf", "application/pdf", ArtifactType, recordId, ct));
            if (!string.Equals(stored[0].Sha256, Convert.ToHexString(SHA256.HashData(rendered.Docx)), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(stored[1].Sha256, Convert.ToHexString(SHA256.HashData(pdf)), StringComparison.OrdinalIgnoreCase))
                throw Rule("HandoverStorageHashMismatch", "Tệp lưu không khớp nội dung render.");
            db.TblFileStorages.AddRange(stored.Select(x => FileRow(x, ArtifactType, recordId, employeeId)));
            var fieldSources = await db.TblContractTemplateFields.AsNoTracking().Where(x => x.TemplateVersionId == row.TemplateVersionId)
                .ToDictionaryAsync(x => x.PlaceholderKey, x => x.SourceFieldKey, ct);
            db.TblContractHandoverPlaceholderValues.AddRange(rendered.Scalars.Select(x => new TblContractHandoverPlaceholderValue {
                HandoverRecordId = recordId, PlaceholderKey = x.Key, SourceFieldKey = fieldSources.GetValueOrDefault(x.Key) ?? "system." + x.Key, RenderedValue = x.Value }));
            using var bytes = new MemoryStream(); using (var writer = new BinaryWriter(bytes, Encoding.UTF8, true)) {
                writer.Write(contractId); writer.Write(row.ContractVersionId); writer.Write(row.HandoverBaselineId); writer.Write(row.TemplateVersionId);
                writer.Write((byte)detail.ContractDirection); writer.Write((byte)detail.LanguageMode); writer.Write(row.HandoverCode);
                writer.Write(row.HandoverDate.Ticks); writer.Write(row.ContractSignDate.Ticks); writer.Write(row.Location); writer.Write(row.LocationEn ?? "");
                writer.Write(lines.Count);
                foreach (var line in lines) { writer.Write(line.HandoverScopeItemId); writer.Write(line.Quantity); writer.Write(line.NotesVi ?? ""); writer.Write(line.NotesEn ?? ""); }
                writer.Write(rendered.Scalars.Count);
                foreach (var field in rendered.Scalars.OrderBy(x => x.Key, StringComparer.Ordinal)) { writer.Write(field.Key); writer.Write(field.Value); }
                writer.Write(detail.References.Count);
                foreach (var reference in detail.References) { writer.Write(reference.TextVi); writer.Write(reference.TextEn ?? ""); }
                writer.Write(detail.Sections.Count);
                foreach (var section in detail.Sections) { writer.Write(section.SectionCode); writer.Write(section.TitleVi); writer.Write(section.TitleEn ?? ""); writer.Write(section.ContentVi ?? ""); writer.Write(section.ContentEn ?? ""); writer.Write(section.BilingualContentMode); }
                writer.Write(SHA256.HashData(rendered.Docx)); writer.Write(SHA256.HashData(pdf));
            }
            row.SnapshotHash = Convert.ToHexString(SHA256.HashData(bytes.ToArray())).ToLowerInvariant();
            row.Status = (byte)HandoverRecordStatus.Finalized; row.FinalizedAt = DateTime.UtcNow; row.FinalizedByEmployeeId = employeeId; Touch(row, employeeId);
            StageAudit(row, employeeId, ContractAuditActionTypes.HandoverFinalized, lines.Count);
            await StageNotificationAsync(row, employeeId, "HandoverFinalized", "Biên bản bàn giao đã chốt, chờ bản ký", ct);
            await db.SaveChangesAsync(ct);
            return await GetAsync(contractId, recordId, employeeId, ct);
        }, ct, async () => { foreach (var file in stored) await files.DeleteAsync(file.TenantCode, file.StorageKey, CancellationToken.None); stored.Clear(); });
    }
    public async Task<HandoverDetailResponse> SignAsync(int contractId, int recordId, UploadHandoverSignedRequest request, int employeeId, CancellationToken ct = default)
    {
        await authorization.EnsureCanManageAcceptanceAsync(contractId, employeeId, ct);
        var policy = PrivateFileUploadPolicies.ContractEvidence();
        if (!request.ConfirmBothPartiesSigned || request.File is null || request.File.Length <= 0 || request.File.Length > policy.MaximumSizeBytes ||
            !policy.AllowedExtensions.Contains(Path.GetExtension(request.File.FileName).ToLowerInvariant()) || !policy.AllowedContentTypes.Contains(request.File.ContentType))
            throw Rule("HandoverEvidenceInvalid", "Cần xác nhận bản ký hai bên và tải tệp PDF/JPG/PNG hợp lệ, tối đa 20 MB.");
        StoredPrivateFile? stored = null;
        return await Execute(contractId, async () => {
            var row = await RecordAsync(contractId, recordId, ct); Match(row.RowVersion, request.RowVersion);
            var source = await SourceAsync(contractId, row.ContractVersionId, true, ct);
            if (row.Status != (byte)HandoverRecordStatus.Finalized) throw Rule("HandoverNotFinalized", "Chỉ tải bản ký cho biên bản đã chốt chờ ký.");
            if (request.SignDate == default || request.SignDate.Date < row.HandoverDate ||
                request.SignDate.Date > DateTime.UtcNow.AddHours(7).Date) throw Rule("HandoverSignDateInvalid", "Ngày ký thực tế không được trước ngày bàn giao hoặc nằm trong tương lai.");
            var artifacts = await db.TblFileStorages.AsNoTracking().Where(x => x.ObjectType == ArtifactType && x.ObjectId == recordId).ToListAsync(ct);
            if (row.SnapshotHash?.Length != 64 || artifacts.Count != 2 || !artifacts.Any(x => x.FileType == "docx") || !artifacts.Any(x => x.FileType == "pdf"))
                throw Rule("HandoverArtifactsMissing", "Biên bản thiếu tệp chốt hoặc snapshot hash.");
            foreach (var artifact in artifacts) _ = await ReadFileAsync(artifact, ct);
            var lines = await db.TblContractHandoverLines.AsNoTracking().Where(x => x.HandoverRecordId == recordId).ToListAsync(ct);
            await ValidateLinesAsync(source, row.HandoverBaselineId, lines.Select(x => new HandoverLineInput(x.HandoverScopeItemId, x.Quantity)).ToArray(), recordId, ct);
            await using var stream = request.File.OpenReadStream();
            stored = await files.SaveAsync(new(stream, request.File.FileName, request.File.ContentType, request.File.Length,
                tenant.GetRequiredTenant().TenantCode, EvidenceType, recordId, policy), ct);
            var file = FileRow(stored, EvidenceType, recordId, employeeId); db.TblFileStorages.Add(file); await db.SaveChangesAsync(ct);
            db.TblContractHandoverEvidences.Add(new() { HandoverRecordId = recordId, FileId = file.FileId, SignDate = request.SignDate.Date,
                UploadedByEmployeeId = employeeId, UploadedAt = DateTime.UtcNow });
            row.Status = (byte)HandoverRecordStatus.Signed; row.SignedAt = DateTime.UtcNow; row.SignedByEmployeeId = employeeId; Touch(row, employeeId);
            StageAudit(row, employeeId, ContractAuditActionTypes.HandoverSigned, lines.Count);
            await StageNotificationAsync(row, employeeId, "HandoverSigned", "Đã ký biên bản bàn giao và ghi nhận phạm vi đã giao", ct);
            await db.SaveChangesAsync(ct); return await GetAsync(contractId, recordId, employeeId, ct);
        }, ct, async () => { if (stored is not null) await files.DeleteAsync(stored.TenantCode, stored.StorageKey, CancellationToken.None); stored = null; });
    }
    public async Task<HandoverDetailResponse> CancelAsync(int contractId, int recordId, CancelContractHandoverRequest request, int employeeId, CancellationToken ct = default)
    {
        await authorization.EnsureCanManageAcceptanceAsync(contractId, employeeId, ct);
        var reason = Text(request.Reason, 1000, "Lý do hủy");
        return await Execute(contractId, async () => {
            var row = await RecordAsync(contractId, recordId, ct); Match(row.RowVersion, request.RowVersion);
            await SourceAsync(contractId, row.ContractVersionId, true, ct);
            if (row.Status is not (0 or 1)) throw Rule("HandoverCannotCancel", "Chỉ hủy bản nháp hoặc bản chốt chờ ký; không sửa/hủy bản đã ký.");
            row.Status = (byte)HandoverRecordStatus.Cancelled; row.CancellationReason = reason; row.CancelledAt = DateTime.UtcNow; row.CancelledByEmployeeId = employeeId; Touch(row, employeeId);
            StageAudit(row, employeeId, ContractAuditActionTypes.HandoverCancelled, await db.TblContractHandoverLines.CountAsync(x => x.HandoverRecordId == recordId, ct));
            await StageNotificationAsync(row, employeeId, "HandoverCancelled", "Biên bản bàn giao đã hủy, phạm vi chờ ký được giải phóng", ct);
            await db.SaveChangesAsync(ct); return await GetAsync(contractId, recordId, employeeId, ct);
        }, ct);
    }
    public async Task<ContractAcceptanceDocumentContent> DownloadAsync(int contractId, int recordId, string format, bool evidence, int employeeId, CancellationToken ct = default)
    {
        await authorization.EnsureCanReadAsync(contractId, employeeId, ct);
        var row = await RecordAsync(contractId, recordId, ct);
        TblFileStorage? file;
        if (evidence)
            file = await (from e in db.TblContractHandoverEvidences.AsNoTracking()
                join f in db.TblFileStorages.AsNoTracking() on e.FileId equals f.FileId
                where e.HandoverRecordId == recordId && f.ObjectType == EvidenceType && f.ObjectId == recordId select f).SingleOrDefaultAsync(ct);
        else { var normalized = Format(format); file = await db.TblFileStorages.AsNoTracking().SingleOrDefaultAsync(x => x.ObjectType == ArtifactType && x.ObjectId == recordId && x.FileType == normalized, ct); }
        if (file is null || row.Status == (byte)HandoverRecordStatus.Draft) throw Rule("HandoverFileMissing", "Biên bản chưa có tệp tương ứng.");
        return new(await ReadFileAsync(file, ct), file.FileName, file.ContentType!);
    }
}
