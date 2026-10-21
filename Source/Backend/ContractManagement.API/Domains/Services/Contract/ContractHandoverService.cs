using System.Data;
using ContractManagement.API.Domains.Notifications;
using System.Security.Cryptography;
using ContractManagement.API.Common.Enums;
using ContractManagement.API.Common.Exceptions;
using ContractManagement.API.Domains.DTOs.Requests.Contract;
using ContractManagement.API.Domains.DTOs.Responses.Contract;
using ContractManagement.Common.Enums;
using ContractManagement.Domains.Interfaces.Contract;
using ContractManagement.Domains.Interfaces.ContractTemplate;
using ContractManagement.Domains.Interfaces.File;
using ContractManagement.Domains.Policies.Contract;
using ContractManagement.Domains.Policies.ContractTemplate;
using ContractManagement.Domains.Services.ContractTemplate;
using ContractManagement.Domains.Services.File;
using ContractManagement.Infrastructure.MultiTenancy.Interfaces;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Domains.Services.Contract;

public sealed partial class ContractHandoverService(
    DbDtctechContext db, IContractResourceAuthorizationService authorization,
    IContractAuditWriter audit, IPrivateFileStorage files, ICurrentTenant tenant,
    IContractTemplatePreviewRenderer renderer, IContractTemplatePdfRenderer pdfRenderer,
    IContractPlaceholderSourceRegistry sources, ContractNotificationProducer? notifications = null) : IContractHandoverService
{
    private const string ArtifactType = "ContractHandoverArtifact";
    private const string EvidenceType = "ContractHandoverEvidence";
    private const string DocxType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private bool Memory => db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory";
    private static BusinessRuleException Rule(string code, string message) => new(StatusCodes.Status409Conflict, code, message);
    private static string Text(string? value, int max, string label)
    {
        var text = value?.Trim();
        if (string.IsNullOrWhiteSpace(text) || text.Length > max) throw Rule("HandoverInputInvalid", label + " là bắt buộc và tối đa " + max + " ký tự.");
        return text;
    }
    private static string? Optional(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return value.Trim().Length <= max ? value.Trim() : throw Rule("HandoverInputInvalid", "Nội dung vượt độ dài cho phép.");
    }
    private static void Match(byte[] actual, string expected)
    {
        byte[] decoded;
        try { decoded = Convert.FromBase64String(expected); }
        catch { throw Rule("StaleRowVersion", "Dữ liệu đã thay đổi. Vui lòng tải lại."); }
        if (!actual.SequenceEqual(decoded)) throw Rule("StaleRowVersion", "Dữ liệu đã thay đổi. Vui lòng tải lại.");
    }
    private void Touch(TblContractHandoverRecord row, int employee)
    {
        row.UpdatedEmployeeId = employee; row.UpdatedAt = DateTime.UtcNow;
        if (Memory) row.RowVersion = Guid.NewGuid().ToByteArray();
    }
    private async Task<T> Execute<T>(int contractId, Func<Task<T>> action, CancellationToken ct, Func<Task>? cleanup = null)
    {
        async Task<T> Run()
        {
            await using var tx = Memory ? null : await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                if (!Memory)
                    await db.TblContracts.FromSqlInterpolated($"SELECT * FROM [tbl_Contract] WITH (UPDLOCK,HOLDLOCK) WHERE [ContractId] = {contractId}").LoadAsync(ct);
                var result = await action();
                if (tx is not null) await tx.CommitAsync(ct);
                return result;
            }
            catch (Exception ex)
            {
                if (tx is not null) await tx.RollbackAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
                if (cleanup is not null) await cleanup();
                if (ex is DbUpdateConcurrencyException) throw Rule("StaleRowVersion", "Biên bản đã thay đổi. Vui lòng tải lại.");
                if (ex is DbUpdateException { InnerException: SqlException sql } && sql.Number is 2601 or 2627)
                    throw Rule("HandoverConflict", "Mã biên bản đã tồn tại hoặc đã có biên bản nháp/chờ ký. Vui lòng tải lại.");
                throw;
            }
        }
        return Memory ? await Run() : await db.Database.CreateExecutionStrategy().ExecuteAsync(Run);
    }
    private sealed record Source(TblContract Contract, TblContractVersion Version, TblContractVersionLegalSnapshot Legal,
        IReadOnlyList<TblContractVersionPartySnapshot> Parties);
    private async Task<Source> SourceAsync(int contractId, int? versionId, bool writable, CancellationToken ct)
    {
        var contract = await db.TblContracts.AsNoTracking().SingleOrDefaultAsync(x => x.ContractId == contractId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy hợp đồng.");
        var id = versionId ?? contract.CurrentVersionId ?? throw Rule("HandoverSourceMissing", "Hợp đồng chưa có phiên bản.");
        var version = await db.TblContractVersions.AsNoTracking().SingleOrDefaultAsync(x => x.ContractId == contractId && x.VersionId == id, ct)
            ?? throw Rule("HandoverSourceMismatch", "Phiên bản không thuộc hợp đồng.");
        var legal = await db.TblContractVersionLegalSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.ContractId == contractId && x.VersionId == id, ct)
            ?? throw Rule("HandoverSourceMissing", "Hợp đồng chưa có snapshot đã ký.");
        ContractPartyRoleResolver.Validate((ContractDirection)legal.ContractDirection);
        if (legal.ContractDirection != contract.ContractDirection || legal.LanguageMode != contract.LanguageMode || !version.IsLocked)
            throw Rule("HandoverSourceMismatch", "Snapshot không khớp chiều/ngôn ngữ hoặc phiên bản chưa khóa.");
        if (writable)
        {
            if (contract.Status != (byte)ContractStatus.Signed || contract.CurrentVersionId != id || !contract.SignDate.HasValue)
                throw Rule("HandoverSourceNotSigned", "Chỉ thao tác bàn giao trên phiên bản hiện hành của hợp đồng đã ký, chưa đóng/hủy.");
            var validEvidence = await (from evidence in db.TblContractSignedEvidences.AsNoTracking()
                join file in db.TblFileStorages.AsNoTracking() on evidence.FileId equals file.FileId
                where evidence.ContractId == contractId && evidence.VersionId == id && evidence.Status == (byte)SignedEvidenceStatus.Active
                    && file.ObjectType == "ContractSignedEvidence" && file.ObjectId == contractId
                    && file.Sha256 != null && file.Sha256.Length == 64 && file.StorageKey != null && file.TenantCode != null
                select evidence.SignedEvidenceId).AnyAsync(ct);
            if (!validEvidence) throw Rule("HandoverSignedEvidenceMissing", "Hợp đồng chưa có bản ký hợp lệ.");
        }
        var parties = await db.TblContractVersionPartySnapshots.AsNoTracking()
            .Where(x => x.ContractVersionLegalSnapshotId == legal.ContractVersionLegalSnapshotId).ToListAsync(ct);
        if (parties.Count != 2 || !parties.Any(x => x.PartyRole == 1) || !parties.Any(x => x.PartyRole == 2))
            throw Rule("HandoverSourceMissing", "Snapshot hợp đồng thiếu hai chủ thể.");
        return new(contract, version, legal, parties);
    }
    private async Task<TblContractHandoverRecord> RecordAsync(int contractId, int recordId, CancellationToken ct) =>
        await db.TblContractHandoverRecords.SingleOrDefaultAsync(x => x.ContractId == contractId && x.HandoverRecordId == recordId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy biên bản bàn giao.");
    private async Task<TblContractHandoverRecord> DraftAsync(int contractId, int id, string rowVersion, CancellationToken ct)
    {
        var row = await RecordAsync(contractId, id, ct); Match(row.RowVersion, rowVersion);
        if (row.Status != (byte)HandoverRecordStatus.Draft) throw Rule("HandoverNotDraft", "Chỉ sửa biên bản bàn giao bản nháp.");
        await SourceAsync(contractId, row.ContractVersionId, true, ct);
        return row;
    }
    private static HandoverRecordSummary Summary(TblContractHandoverRecord row) => new(row.HandoverRecordId,
        row.ContractVersionId, row.HandoverCode, row.HandoverDate, (HandoverRecordStatus)row.Status, Convert.ToBase64String(row.RowVersion));
    public async Task<IReadOnlyList<HandoverRecordSummary>> ListAsync(int contractId, int employeeId, CancellationToken ct = default)
    {
        await authorization.EnsureCanReadAsync(contractId, employeeId, ct);
        return (await db.TblContractHandoverRecords.AsNoTracking().Where(x => x.ContractId == contractId)
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.HandoverRecordId).ToListAsync(ct)).Select(Summary).ToArray();
    }
    public async Task<IReadOnlyList<HandoverTemplateOption>> TemplatesAsync(int contractId, int employeeId, CancellationToken ct = default)
    {
        await authorization.EnsureCanReadAsync(contractId, employeeId, ct);
        var source = await SourceAsync(contractId, null, false, ct);
        return await (from v in db.TblContractTemplateVersions.AsNoTracking()
            join t in db.TblContractTemplates.AsNoTracking() on v.TemplateId equals t.TemplateId
            where t.IsActive && t.DocumentType == (byte)TemplateDocumentType.HandoverRecord
                && t.CurrentPublishedVersionId == v.TemplateVersionId && v.Status == (byte)TemplateVersionStatus.Published
                && v.ValidationStatus == (byte)TemplateValidationStatus.Valid && v.DocumentFileId != null
                && v.ContractDirection == source.Legal.ContractDirection && t.LanguageMode == source.Legal.LanguageMode
            orderby t.TemplateName
            select new HandoverTemplateOption(v.TemplateVersionId, t.TemplateName, v.VersionNo,
                (ContractDirection)v.ContractDirection, (ContractLanguageMode)t.LanguageMode)).ToListAsync(ct);
    }
    public async Task<IReadOnlyList<HandoverScopeCandidateResponse>> ScopeCandidatesAsync(int contractId, int employeeId, CancellationToken ct = default)
    {
        await authorization.EnsureCanReadAsync(contractId, employeeId, ct);
        var source = await SourceAsync(contractId, null, false, ct);
        return await db.TblContractItems.AsNoTracking().Where(x => x.ContractId == contractId && x.VersionId == source.Version.VersionId)
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.ContractItemId)
            .Select(x => new HandoverScopeCandidateResponse(x.ContractItemId, x.ItemName, x.ItemNameEn, x.UnitName, x.UnitNameEn, x.Quantity)).ToListAsync(ct);
    }
    public async Task<HandoverReadinessResponse> ReadinessAsync(int contractId, int employeeId, CancellationToken ct = default)
    {
        await authorization.EnsureCanReadAsync(contractId, employeeId, ct);
        var contract = await db.TblContracts.AsNoTracking().SingleOrDefaultAsync(x => x.ContractId == contractId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy hợp đồng.");
        var versionId = contract.CurrentVersionId ?? 0;
        var baseline = await db.TblContractHandoverBaselines.AsNoTracking().SingleOrDefaultAsync(x => x.ContractId == contractId && x.ContractVersionId == versionId, ct);
        var active = await db.TblContractHandoverRecords.AsNoTracking().Where(x => x.ContractId == contractId && x.ContractVersionId == versionId && (x.Status == 0 || x.Status == 1))
            .Select(x => (int?)x.HandoverRecordId).SingleOrDefaultAsync(ct);
        var progress = await ContractHandoverProgressReader.ReadAsync(db, contractId, versionId, tenant.GetRequiredTenant().TenantCode, ct);
        if (progress.IntegrityError is not null) throw Rule("HandoverCoverageInvalid", progress.IntegrityError);
        var response = progress.Items;
        var completed = progress.DeliveredCompleted;
        var blocker = contract.Status != (byte)ContractStatus.Signed ? "HandoverSourceNotSigned"
            : active.HasValue ? "HandoverRecordInProgress" : baseline is null ? "HandoverScopeNotConfirmed" : completed ? "HandoverAlreadyComplete" : null;
        var message = blocker switch {
            "HandoverSourceNotSigned" => "Hợp đồng phải đã ký và chưa đóng/hủy.",
            "HandoverRecordInProgress" => "Hãy tiếp tục hoặc hủy biên bản nháp/chờ ký đang có.",
            "HandoverScopeNotConfirmed" => "Cần xác nhận cách theo dõi phạm vi từ các hạng mục đã ký.",
            "HandoverAlreadyComplete" => "Đã bàn giao đủ phạm vi.", _ => null };
        if (contract.Status == (byte)ContractStatus.Signed) {
            try { await SourceAsync(contractId, versionId, true, ct); }
            catch (BusinessRuleException error) { blocker = error.Code; message = error.Message; }
        }
        return new(versionId, baseline?.HandoverBaselineId, completed, blocker is null, blocker, message, active, response);
    }
    private async Task StageNotificationAsync(TblContractHandoverRecord row, int employeeId, string eventCode, string title, CancellationToken ct)
    {
        if (notifications is null) return;
        await notifications.StageCurrentOwnerAsync(row.ContractId, employeeId,
            $"contract:{row.ContractId}:handover:{row.HandoverRecordId}:{eventCode}", eventCode,
            title, "handover", DateTime.UtcNow, ct);
    }
    private void StageAudit(TblContractHandoverRecord row, int employee, string action, int lineCount)
    {
        audit.StageEmployeeAudits([new(row.ContractId, row.ContractVersionId, employee, action, ContractAuditResults.Succeeded,
            DateTime.UtcNow, SubjectType: ContractAuditSubjectTypes.HandoverRecord, SubjectId: row.HandoverRecordId,
            NewValues: ContractAuditValues.Create(("HandoverRecordId", row.HandoverRecordId), ("HandoverBaselineId", row.HandoverBaselineId),
                ("HandoverLineCount", lineCount), ("HandoverStatus", row.Status), ("CurrentVersionId", row.ContractVersionId),
                ("TemplateVersionId", row.TemplateVersionId), ("SnapshotHash", row.SnapshotHash)))]);
    }
}
