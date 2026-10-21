using ContractManagement.API.Common.Enums;
using ContractManagement.API.Domains.DTOs.Responses.Contract;
using ContractManagement.Domains.Policies.Contract;
using ContractManagement.Infrastructure.Persistence.Application;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Domains.Services.Contract;

internal sealed record ContractHandoverProgress(bool BaselineConfirmed,
    IReadOnlyList<HandoverScopeItemResponse> Items, bool DeliveredCompleted, bool AcceptedCompleted,
    bool HandoverInProgress, bool AcceptanceInProgress, string? IntegrityError)
{
    public int DeliveredItemCount => Items.Count(x => x.Delivered == x.RequiredQuantity);
    public int AcceptedItemCount => Items.Count(x => x.Accepted == x.RequiredQuantity);
}

/// <summary>One source of truth for handover, acceptance and completion. No cached boolean or money-to-quantity mapping.</summary>
internal static class ContractHandoverProgressReader
{
    internal static async Task<ContractHandoverProgress> ReadAsync(DbDtctechContext db, int contractId,
        int versionId, string tenantCode, CancellationToken ct)
    {
        var baseline = await db.TblContractHandoverBaselines.AsNoTracking().SingleOrDefaultAsync(
            x => x.ContractId == contractId && x.ContractVersionId == versionId, ct);
        var scope = baseline is null ? [] : await db.TblContractHandoverScopeItems.AsNoTracking()
            .Where(x => x.HandoverBaselineId == baseline.HandoverBaselineId).OrderBy(x => x.DisplayOrder).ToListAsync(ct);
        var records = await db.TblContractHandoverRecords.AsNoTracking()
            .Where(x => x.ContractId == contractId && x.ContractVersionId == versionId)
            .Select(x => new { x.HandoverRecordId, x.HandoverBaselineId, x.Status }).ToListAsync(ct);
        var acceptances = await db.TblContractAcceptanceRecords.AsNoTracking()
            .Where(x => x.ContractId == contractId && x.ContractVersionId == versionId)
            .Select(x => new { x.AcceptanceRecordId, x.Status }).ToListAsync(ct);
        var activeHandover = records.Any(x => x.Status is 0 or 1);
        var activeAcceptance = acceptances.Any(x => x.Status is 0 or 1);
        ContractHandoverProgress Invalid(string message) => new(baseline is not null, [], false, false,
            activeHandover, activeAcceptance, message);
        var recordIds = records.Select(x => x.HandoverRecordId).ToArray();
        var validSigned = await (from proof in db.TblContractHandoverEvidences.AsNoTracking()
            join file in db.TblFileStorages.AsNoTracking() on proof.FileId equals file.FileId
            where recordIds.Contains(proof.HandoverRecordId) && file.ObjectType == "ContractHandoverEvidence"
                && file.ObjectId == proof.HandoverRecordId && file.TenantCode == tenantCode
                && file.StorageKey != null && file.StorageKey != "" && file.Sha256 != null && file.Sha256.Length == 64 && file.FileSize > 0
            select proof.HandoverRecordId).ToListAsync(ct);
        if (records.Any(x => x.Status == 2 && !validSigned.Contains(x.HandoverRecordId)))
            return Invalid("Bàn giao đã ký thiếu metadata bản ký hợp lệ của tenant hiện tại.");
        var lines = await db.TblContractHandoverLines.AsNoTracking().Where(x => recordIds.Contains(x.HandoverRecordId)).ToListAsync(ct);
        var scopeIds = scope.Select(x => x.HandoverScopeItemId).ToHashSet();
        if (lines.Any(x => !scopeIds.Contains(x.HandoverScopeItemId)
            || records.Single(r => r.HandoverRecordId == x.HandoverRecordId).HandoverBaselineId != baseline?.HandoverBaselineId))
            return Invalid("Dòng bàn giao không thuộc baseline của phiên bản hợp đồng.");
        var acceptanceIds = acceptances.Select(x => x.AcceptanceRecordId).ToArray();
        var validAccepted = await (from proof in db.TblContractAcceptanceEvidences.AsNoTracking()
            join file in db.TblFileStorages.AsNoTracking() on proof.FileId equals file.FileId
            where acceptanceIds.Contains(proof.AcceptanceRecordId) && file.ObjectType == "ContractAcceptanceEvidence"
                && file.ObjectId == contractId && file.TenantCode == tenantCode
                && file.StorageKey != null && file.StorageKey != "" && file.Sha256 != null && file.Sha256.Length == 64 && file.FileSize > 0
            select proof.AcceptanceRecordId).ToListAsync(ct);
        if (acceptances.Any(x => x.Status == 2 && !validAccepted.Contains(x.AcceptanceRecordId)))
            return Invalid("Nghiệm thu đã ký thiếu metadata bản ký hợp lệ của tenant hiện tại.");
        var allocations = await db.TblContractAcceptanceScopeAllocations.AsNoTracking()
            .Where(x => acceptanceIds.Contains(x.AcceptanceRecordId)).ToListAsync(ct);
        var counted = allocations.Where(x => acceptances.Single(r => r.AcceptanceRecordId == x.AcceptanceRecordId).Status is 1 or 2).ToList();
        foreach (var allocation in counted)
        {
            var source = lines.SingleOrDefault(x => x.HandoverLineId == allocation.HandoverLineId);
            if (source is null || records.Single(x => x.HandoverRecordId == source.HandoverRecordId).Status != 2)
                return Invalid("Nghiệm thu đã chốt/ký tham chiếu dòng bàn giao chưa ký hoặc ngoài phiên bản.");
            var mode = (HandoverScopeMode)scope.Single(x => x.HandoverScopeItemId == source.HandoverScopeItemId).ScopeMode;
            try { HandoverCoveragePolicy.Quantity(allocation.Quantity, mode); }
            catch (ArgumentException) { return Invalid("Khối lượng nghiệm thu không hợp lệ."); }
        }
        foreach (var line in lines)
            if (counted.Where(x => x.HandoverLineId == line.HandoverLineId).Sum(x => x.Quantity) > line.Quantity)
                return Invalid("Tổng nghiệm thu và giữ chỗ vượt lượng của dòng bàn giao nguồn.");
        var result = new List<HandoverScopeItemResponse>();
        foreach (var item in scope)
        {
            var itemLines = lines.Where(x => x.HandoverScopeItemId == item.HandoverScopeItemId).ToList();
            try
            {
                foreach (var line in itemLines.Where(x => records.Single(r => r.HandoverRecordId == x.HandoverRecordId).Status is 1 or 2))
                    HandoverCoveragePolicy.Quantity(line.Quantity, (HandoverScopeMode)item.ScopeMode);
                var coverage = HandoverCoveragePolicy.Evaluate(item.RequiredQuantity,
                    itemLines.Select(l => ((HandoverRecordStatus)records.Single(r => r.HandoverRecordId == l.HandoverRecordId).Status, l.Quantity)),
                    counted.Where(a => itemLines.Any(l => l.HandoverLineId == a.HandoverLineId))
                        .Select(a => ((AcceptanceRecordStatus)acceptances.Single(r => r.AcceptanceRecordId == a.AcceptanceRecordId).Status, a.Quantity)));
                result.Add(new(item.HandoverScopeItemId, item.SourceContractItemId, item.ItemCode, item.NameVi, item.NameEn,
                    item.UnitVi, item.UnitEn, (HandoverScopeMode)item.ScopeMode, item.RequiredQuantity, coverage.Delivered,
                    coverage.Reserved, coverage.Remaining, coverage.Accepted, coverage.AcceptanceReserved, item.SourceQuantity));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            { return Invalid("Phạm vi bàn giao/nghiệm thu vượt nghĩa vụ đã ký hoặc không nhất quán."); }
        }
        return new(baseline is not null, result, result.Count > 0 && result.All(x => x.Delivered == x.RequiredQuantity),
            result.Count > 0 && result.All(x => x.Accepted == x.RequiredQuantity),
            activeHandover, activeAcceptance, null);
    }
}
