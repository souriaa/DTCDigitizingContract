using ContractManagement.API.Common.Enums;
using ContractManagement.Infrastructure.Persistence.Application;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Domains.Services.Contract;

internal sealed record ContractAcceptanceProgress(
    bool Completed,
    bool HasSignedAcceptance,
    bool HasSignedFinal,
    bool HasPartialHistory,
    int AcceptedMilestoneCount,
    IReadOnlyList<int> RemainingMilestoneIds,
    ContractHandoverProgress Scope);

internal static class ContractAcceptanceProgressReader
{
    private const string EvidenceObjectType = "ContractAcceptanceEvidence";

    internal static async Task<ContractAcceptanceProgress> ReadAsync(
        DbDtctechContext db,
        int contractId,
        int versionId,
        string tenantCode,
        IReadOnlyList<int> milestoneIds,
        CancellationToken ct)
    {
        var signedRecords = await (
            from record in db.TblContractAcceptanceRecords.AsNoTracking()
            join evidence in db.TblContractAcceptanceEvidences.AsNoTracking()
                on record.AcceptanceRecordId equals evidence.AcceptanceRecordId
            join file in db.TblFileStorages.AsNoTracking()
                on evidence.FileId equals file.FileId
            where record.ContractId == contractId
                && record.ContractVersionId == versionId
                && record.Status == (byte)AcceptanceRecordStatus.Signed
                && file.ObjectType == EvidenceObjectType
                && file.ObjectId == contractId && file.TenantCode == tenantCode
                && file.StorageKey != null && file.StorageKey != ""
                && file.Sha256 != null && file.Sha256.Length == 64
                && file.FileSize > 0
            select new { record.AcceptanceRecordId, record.AcceptanceKind })
            .Distinct()
            .ToListAsync(ct);
        var hasPartialHistory = await db.TblContractAcceptanceRecords
            .AsNoTracking().AnyAsync(x => x.ContractId == contractId
                && x.ContractVersionId == versionId
                && x.AcceptanceKind == (byte)AcceptanceKind.Partial, ct);
        var hasSignedFinal = !hasPartialHistory && signedRecords.Any(x =>
            x.AcceptanceKind == (byte)AcceptanceKind.Final);
        var signedPartialIds = signedRecords
            .Where(x => x.AcceptanceKind == (byte)AcceptanceKind.Partial)
            .Select(x => x.AcceptanceRecordId)
            .ToArray();
        List<int> acceptedIds = signedPartialIds.Length == 0
            ? []
            : await db.TblContractAcceptanceMilestones.AsNoTracking()
                .Where(x => signedPartialIds.Contains(x.AcceptanceRecordId))
                .Select(x => x.PaymentMilestoneId)
                .Distinct()
                .ToListAsync(ct);
        var acceptedSet = acceptedIds.ToHashSet();
        int[] remaining = hasSignedFinal
            ? []
            : milestoneIds.Where(id => !acceptedSet.Contains(id)).ToArray();
        var scope = await ContractHandoverProgressReader.ReadAsync(db, contractId, versionId, tenantCode, ct);
        var milestonesCompleted = hasSignedFinal
            || remaining.Length == 0
                && signedPartialIds.Length > 0;
        return new ContractAcceptanceProgress(
            milestonesCompleted && scope.AcceptedCompleted && scope.DeliveredCompleted && scope.IntegrityError is null,
            signedRecords.Count > 0,
            hasSignedFinal,
            hasPartialHistory,
            milestoneIds.Count - remaining.Length,
            remaining, scope);
    }
}
