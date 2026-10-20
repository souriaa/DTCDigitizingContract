using ContractManagement.API.Common.Enums;
using ContractManagement.API.Domains.DTOs.Requests.Contract;
using ContractManagement.API.Domains.DTOs.Responses.Contract;
using ContractManagement.Domains.Policies.Contract;
using ContractManagement.Domains.Interfaces.Contract;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Domains.Services.Contract;

public sealed partial class ContractAcceptanceService
{
    private async Task<bool> HasRemainingAcceptanceMilestonesAsync(int contractId, int versionId, CancellationToken ct)
    {
        var ids = await _db.TblContractPaymentMilestones.AsNoTracking().Where(x => x.ContractId == contractId && x.VersionId == versionId)
            .Select(x => x.PaymentMilestoneId).ToListAsync(ct);
        var progress = await ContractAcceptanceProgressReader.ReadAsync(_db, contractId, versionId, _tenant.GetRequiredTenant().TenantCode, ids, ct);
        return progress.RemainingMilestoneIds.Count > 0;
    }

    private async Task<bool> AcceptanceScopeCompleteAsync(int contractId, int versionId, CancellationToken ct)
    {
        var scope = await ContractHandoverProgressReader.ReadAsync(_db, contractId, versionId, _tenant.GetRequiredTenant().TenantCode, ct);
        return scope.IntegrityError is null && scope.DeliveredCompleted && scope.AcceptedCompleted;
    }

    private async Task LockHandoverContractAsync(int contractId, CancellationToken ct)
    {
        // Same lock and lock order as handover mutations; allocations cannot race signed coverage.
        if (!UsesInMemoryProvider())
            await _db.TblContracts.FromSqlInterpolated(
                $"SELECT * FROM [tbl_Contract] WITH (UPDLOCK,HOLDLOCK) WHERE [ContractId] = {contractId}").LoadAsync(ct);
    }

    public async Task<AcceptanceHandoverOptionsResponse> HandoverOptionsAsync(
        int contractId, int acceptanceRecordId, int employeeId, CancellationToken ct = default)
    {
        await _authorization.EnsureCanReadAsync(contractId, employeeId, ct);
        var row = await _db.TblContractAcceptanceRecords.AsNoTracking().SingleOrDefaultAsync(
            x => x.ContractId == contractId && x.AcceptanceRecordId == acceptanceRecordId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy biên bản nghiệm thu.");
        return await ReadHandoverOptionsAsync(contractId, row, ct);
    }

    public async Task<ContractAcceptanceRecordResponse> ReplaceHandoverScopeAsync(
        int contractId, int acceptanceRecordId, ReplaceAcceptanceHandoverScopeRequest request,
        int employeeId, CancellationToken ct = default)
    {
        await _authorization.EnsureCanManageAcceptanceAsync(contractId, employeeId, ct);
        return await ExecuteAsync(async () =>
        {
            var row = await RequireDraftAsync(contractId, acceptanceRecordId, request.RowVersion, ct);
            await EnsureRecordSourceAsync(contractId, row.ContractVersionId, ct);
            var options = await ReadHandoverOptionsAsync(contractId, row, ct);
            if (request.Items.Select(x => x.HandoverLineId).Distinct().Count() != request.Items.Count)
                throw Rule("AcceptanceHandoverDuplicate", "Một dòng bàn giao chỉ được chọn một lần.");
            foreach (var item in request.Items)
            {
                var source = options.Items.SingleOrDefault(x => x.HandoverLineId == item.HandoverLineId)
                    ?? throw Rule("AcceptanceHandoverSourceInvalid", "Chỉ chọn bàn giao đã ký hợp lệ cùng hợp đồng và phiên bản.");
                try { HandoverCoveragePolicy.Quantity(item.Quantity, (HandoverScopeMode)source.ScopeMode); }
                catch (ArgumentException) { throw Rule("AcceptanceHandoverQuantityInvalid", "Khối lượng nghiệm thu phải dương, tối đa 4 số lẻ; nguyên gói phải nghiệm thu đủ 1."); }
                if (item.Quantity > source.AvailableQuantity)
                    throw Rule("AcceptanceHandoverQuantityExceeded", $"Phạm vi {source.ItemCode} chỉ còn {source.AvailableQuantity} {source.UnitVi} có thể nghiệm thu.");
            }
            var old = await _db.TblContractAcceptanceScopeAllocations.Where(x => x.AcceptanceRecordId == acceptanceRecordId).ToListAsync(ct);
            _db.TblContractAcceptanceScopeAllocations.RemoveRange(old);
            await _db.SaveChangesAsync(ct);
            _db.TblContractAcceptanceScopeAllocations.AddRange(request.Items.Select(x => new TblContractAcceptanceScopeAllocation
            { AcceptanceRecordId = acceptanceRecordId, HandoverLineId = x.HandoverLineId, Quantity = x.Quantity }));
            Touch(row, employeeId);
            StageAudit(row, employeeId, ContractAuditActionTypes.AcceptanceRecordUpdated,
                await CountAppendixReferencesAsync(acceptanceRecordId, ct));
            await _db.SaveChangesAsync(ct);
            return await LoadAsync(contractId, acceptanceRecordId, ct);
        }, ct, contractId);
    }

    private async Task<AcceptanceHandoverOptionsResponse> ReadHandoverOptionsAsync(
        int contractId, TblContractAcceptanceRecord row, CancellationToken ct)
    {
        var tenantCode = _tenant.GetRequiredTenant().TenantCode;
        var rows = await (from line in _db.TblContractHandoverLines.AsNoTracking()
            join record in _db.TblContractHandoverRecords.AsNoTracking() on line.HandoverRecordId equals record.HandoverRecordId
            join scope in _db.TblContractHandoverScopeItems.AsNoTracking() on line.HandoverScopeItemId equals scope.HandoverScopeItemId
            join baseline in _db.TblContractHandoverBaselines.AsNoTracking() on scope.HandoverBaselineId equals baseline.HandoverBaselineId
            join proof in _db.TblContractHandoverEvidences.AsNoTracking() on record.HandoverRecordId equals proof.HandoverRecordId
            join file in _db.TblFileStorages.AsNoTracking() on proof.FileId equals file.FileId
            where record.ContractId == contractId && record.ContractVersionId == row.ContractVersionId && record.Status == 2
                && baseline.ContractId == contractId && baseline.ContractVersionId == row.ContractVersionId
                && record.HandoverBaselineId == baseline.HandoverBaselineId
                && file.ObjectType == "ContractHandoverEvidence" && file.ObjectId == record.HandoverRecordId
                && file.TenantCode == tenantCode && file.Sha256 != null && file.Sha256.Length == 64 && file.StorageKey != null
            orderby record.HandoverDate, record.HandoverRecordId, line.DisplayOrder
            select new { line.HandoverLineId, record.HandoverRecordId, line.HandoverScopeItemId,
                record.HandoverCode, record.HandoverDate, proof.SignDate, scope.ItemCode, scope.NameVi, scope.NameEn,
                scope.UnitVi, scope.UnitEn, line.Quantity, scope.ScopeMode }).ToListAsync(ct);
        var lineIds = rows.Select(x => x.HandoverLineId).ToArray();
        var uses = await (from a in _db.TblContractAcceptanceScopeAllocations.AsNoTracking()
            join r in _db.TblContractAcceptanceRecords.AsNoTracking() on a.AcceptanceRecordId equals r.AcceptanceRecordId
            where r.ContractId == contractId && r.ContractVersionId == row.ContractVersionId
                && lineIds.Contains(a.HandoverLineId)
            select new { a.HandoverLineId, a.AcceptanceRecordId, a.Quantity, r.Status }).ToListAsync(ct);
        return new(rows.Select(x => {
            var reserved = uses.Where(a => a.HandoverLineId == x.HandoverLineId &&
                a.AcceptanceRecordId != row.AcceptanceRecordId && a.Status is 1 or 2).Sum(a => a.Quantity);
            return new AcceptanceHandoverScopeResponse(x.HandoverLineId, x.HandoverRecordId, x.HandoverScopeItemId,
                x.HandoverCode, x.HandoverDate, x.SignDate, x.ItemCode, x.NameVi, x.NameEn, x.UnitVi, x.UnitEn,
                x.Quantity, Math.Max(0, x.Quantity - reserved),
                uses.Where(a => a.HandoverLineId == x.HandoverLineId && a.AcceptanceRecordId == row.AcceptanceRecordId).Sum(a => a.Quantity), x.ScopeMode);
        }).ToArray(), rows.Count == 0 ? "Chưa có phạm vi bàn giao đã ký hợp lệ. Có thể soạn nháp nhưng chưa thể chốt nghiệm thu." : null);
    }

    private async Task<(IReadOnlyList<AcceptanceHandoverScopeResponse> Selected, string? Blocker)> ReadHandoverSelectionAsync(
        int contractId, TblContractAcceptanceRecord row, CancellationToken ct)
    {
        var options = await ReadHandoverOptionsAsync(contractId, row, ct);
        var selected = options.Items.Where(x => x.Quantity > 0).ToArray();
        var actualCount = await _db.TblContractAcceptanceScopeAllocations.CountAsync(x => x.AcceptanceRecordId == row.AcceptanceRecordId, ct);
        if (selected.Length != actualCount)
            return (selected, "Có dòng nguồn bàn giao không còn hợp lệ. Vui lòng chọn lại phạm vi.");
        if (selected.Length == 0) return (selected, options.Blocker ?? "Chưa chọn phạm vi bàn giao cần nghiệm thu.");
        var dateError = GetAcceptanceDateError(row.AcceptanceDate, row.ContractSignDate, selected);
        if (dateError is not null) return (selected, dateError);
        if (selected.Any(x => x.Quantity > x.AvailableQuantity))
            return (selected, "Phạm vi đã được biên bản khác giữ chỗ hoặc nghiệm thu. Vui lòng chọn lại.");
        if ((AcceptanceKind)row.AcceptanceKind == AcceptanceKind.Final)
        {
            var baseline = await _db.TblContractHandoverBaselines.AsNoTracking().SingleOrDefaultAsync(
                x => x.ContractId == contractId && x.ContractVersionId == row.ContractVersionId, ct);
            var required = baseline is null ? [] : await _db.TblContractHandoverScopeItems.AsNoTracking()
                .Where(x => x.HandoverBaselineId == baseline.HandoverBaselineId).ToListAsync(ct);
            if (required.Count == 0 || required.Any(s => selected.Where(x => x.HandoverScopeItemId == s.HandoverScopeItemId).Sum(x => x.Quantity) != s.RequiredQuantity))
                return (selected, "Nghiệm thu toàn bộ phải chọn đủ toàn bộ phạm vi bắt buộc đã bàn giao và ký.");
        }
        if ((AcceptanceKind)row.AcceptanceKind == AcceptanceKind.Partial)
        {
            var required = await (from s in _db.TblContractHandoverScopeItems.AsNoTracking()
                join b in _db.TblContractHandoverBaselines.AsNoTracking() on s.HandoverBaselineId equals b.HandoverBaselineId
                where b.ContractId == contractId && b.ContractVersionId == row.ContractVersionId
                select new { s.HandoverScopeItemId, s.RequiredQuantity }).ToListAsync(ct);
            var previous = await (from a in _db.TblContractAcceptanceScopeAllocations.AsNoTracking()
                join r in _db.TblContractAcceptanceRecords.AsNoTracking() on a.AcceptanceRecordId equals r.AcceptanceRecordId
                join l in _db.TblContractHandoverLines.AsNoTracking() on a.HandoverLineId equals l.HandoverLineId
                where r.ContractId == contractId && r.ContractVersionId == row.ContractVersionId && r.Status == 2
                    && r.AcceptanceRecordId != row.AcceptanceRecordId
                select new { l.HandoverScopeItemId, a.Quantity }).ToListAsync(ct);
            var closesScope = required.Count > 0 && required.All(s =>
                previous.Where(x => x.HandoverScopeItemId == s.HandoverScopeItemId).Sum(x => x.Quantity)
                + selected.Where(x => x.HandoverScopeItemId == s.HandoverScopeItemId).Sum(x => x.Quantity) == s.RequiredQuantity);
            if (closesScope)
            {
                var milestones = await _db.TblContractPaymentMilestones.AsNoTracking().Where(x => x.ContractId == contractId && x.VersionId == row.ContractVersionId)
                    .Select(x => x.PaymentMilestoneId).ToListAsync(ct);
                var progress = await ContractAcceptanceProgressReader.ReadAsync(_db, contractId, row.ContractVersionId, _tenant.GetRequiredTenant().TenantCode, milestones, ct);
                var chosen = await _db.TblContractAcceptanceMilestones.AsNoTracking().Where(x => x.AcceptanceRecordId == row.AcceptanceRecordId)
                    .Select(x => x.PaymentMilestoneId).ToListAsync(ct);
                if (progress.RemainingMilestoneIds.Except(chosen).Any())
                    return (selected, "Đây là toàn bộ phạm vi còn lại nhưng vẫn thiếu đợt thanh toán chưa nghiệm thu. Chọn đủ các đợt còn lại hoặc giảm phạm vi để còn vòng tiếp theo.");
            }
        }
        return (selected, null);
    }

    private async Task EnsureHandoverCoverageAsync(int contractId, int recordId, CancellationToken ct)
    {
        var row = await _db.TblContractAcceptanceRecords.AsNoTracking().SingleAsync(x => x.ContractId == contractId && x.AcceptanceRecordId == recordId, ct);
        var selected = await ReadHandoverSelectionAsync(contractId, row, ct);
        ValidateAcceptanceDate(row.AcceptanceDate, row.ContractSignDate, selected.Selected);
        if (selected.Blocker is not null) throw Rule("AcceptanceHandoverCoverageRequired", selected.Blocker,
            StatusCodes.Status409Conflict);
    }

    private static string? GetAcceptanceDateError(DateTime date, DateTime contractSignDate,
        IReadOnlyList<AcceptanceHandoverScopeResponse>? scope = null)
    {
        if (date.Date < contractSignDate.Date)
            return $"Ngày nghiệm thu không được trước ngày ký hợp đồng ({contractSignDate:dd/MM/yyyy}).";
        var source = scope?.OrderByDescending(x => x.HandoverDate).FirstOrDefault(x => date.Date < x.HandoverDate.Date);
        if (source is not null)
        {
            return $"Ngày nghiệm thu không được trước ngày bàn giao của biên bản {source.HandoverCode}. Chọn từ {source.HandoverDate:dd/MM/yyyy}.";
        }
        return null;
    }

    private static void ValidateAcceptanceDate(DateTime date, DateTime contractSignDate,
        IReadOnlyList<AcceptanceHandoverScopeResponse>? scope = null)
    {
        var error = GetAcceptanceDateError(date, contractSignDate, scope);
        if (error is not null) throw Rule("AcceptanceDateInvalid", error);
    }

    private static IEnumerable<ContractAcceptanceReferenceResponse> HandoverReferences(
        IReadOnlyList<AcceptanceHandoverScopeResponse> scope, int start) =>
        scope.GroupBy(x => x.HandoverRecordId).Select((g, index) => {
            var source = g.First();
            var linesVi = string.Join("; ", g.Select(x => $"{x.NameVi}: {x.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture)} {x.UnitVi}"));
            // The source was already validated bilingual before signing; never substitute VI for missing EN.
            var linesEn = string.Join("; ", g.Select(x => $"{x.NameEn}: {x.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture)} {x.UnitEn}"));
            return new ContractAcceptanceReferenceResponse {
                ReferenceType = AcceptanceReferenceType.Handover, HandoverRecordId = source.HandoverRecordId, DisplayOrder = start + index,
                TextVi = $"Căn cứ Biên bản bàn giao số {source.HandoverCode} ký ngày {source.SignDate:dd/MM/yyyy}, phạm vi: {linesVi}.",
                TextEn = $"Pursuant to Handover Record No. {source.HandoverCode} signed on {source.SignDate:dd/MM/yyyy}, scope: {linesEn}."
            };
        });
}
