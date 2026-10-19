using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;
namespace ContractManagement.Infrastructure.Persistence.Application;
public partial class DbDtctechContext
{
    private async Task ValidateHandoverChangesAsync(CancellationToken ct, bool useAsync)
    {
        if (ChangeTracker.Entries<TblContractHandoverBaseline>().Any(IsModifiedOrDeleted) ||
            ChangeTracker.Entries<TblContractHandoverScopeItem>().Any(IsModifiedOrDeleted))
            throw new InvalidOperationException("Phạm vi bàn giao đã xác nhận là snapshot bất biến.");
        foreach (var entry in ChangeTracker.Entries<TblContractHandoverRecord>())
        {
            if (entry.State == EntityState.Deleted)
                throw new InvalidOperationException("Không xóa biên bản bàn giao, chỉ hủy theo vòng đời.");
            if (entry.State != EntityState.Modified) continue;
            var oldStatus = entry.OriginalValues.GetValue<byte>(nameof(TblContractHandoverRecord.Status));
            if (oldStatus is 2 or 3)
                throw new InvalidOperationException("Biên bản bàn giao đã ký/hủy là bất biến.");
            if (oldStatus == 1)
            {
                if (entry.Entity.Status is not (2 or 3))
                    throw new InvalidOperationException("Bản bàn giao chốt chỉ được chuyển sang đã ký hoặc đã hủy.");
                string[] allowed = [nameof(TblContractHandoverRecord.Status), nameof(TblContractHandoverRecord.SignedAt),
                    nameof(TblContractHandoverRecord.SignedByEmployeeId), nameof(TblContractHandoverRecord.CancelledAt),
                    nameof(TblContractHandoverRecord.CancelledByEmployeeId), nameof(TblContractHandoverRecord.CancellationReason),
                    nameof(TblContractHandoverRecord.UpdatedAt), nameof(TblContractHandoverRecord.UpdatedEmployeeId), nameof(TblContractHandoverRecord.RowVersion)];
                if (entry.Properties.Any(x => x.IsModified && !allowed.Contains(x.Metadata.Name)))
                    throw new InvalidOperationException("Không chỉnh nội dung/snapshot của bản bàn giao đã chốt.");
            }
        }
        var ids = ChangeTracker.Entries().Where(entry => (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                && (entry.Entity is TblContractHandoverLine or TblContractHandoverParty or TblContractHandoverReference or TblContractHandoverSection or TblContractHandoverPlaceholderValue))
            .SelectMany(entry => entry.State == EntityState.Added
                ? new[] { entry.CurrentValues.GetValue<int>("HandoverRecordId") }
                : new[] { entry.CurrentValues.GetValue<int>("HandoverRecordId"), entry.OriginalValues.GetValue<int>("HandoverRecordId") })
            .Distinct().ToArray();
        foreach (var id in ids)
        {
            var tracked = ChangeTracker.Entries<TblContractHandoverRecord>().SingleOrDefault(x => x.Entity.HandoverRecordId == id);
            byte? status;
            if (tracked is not null)
                status = tracked.State == EntityState.Added ? (byte)0 : tracked.OriginalValues.GetValue<byte>(nameof(TblContractHandoverRecord.Status));
            else
            {
                var query = TblContractHandoverRecords.AsNoTracking().Where(x => x.HandoverRecordId == id).Select(x => (byte?)x.Status);
                status = useAsync ? await query.SingleOrDefaultAsync(ct) : query.SingleOrDefault();
            }
            if (status is not 0) throw new InvalidOperationException("Nội dung bản bàn giao đã chốt/ký/hủy không được sửa hoặc thêm.");
        }
        foreach (var id in ChangeTracker.Entries<TblContractAcceptanceScopeAllocation>().Where(IsMutated)
            .SelectMany(x => x.State == EntityState.Added
                ? new[] { x.Entity.AcceptanceRecordId }
                : new[] { x.Entity.AcceptanceRecordId, x.OriginalValues.GetValue<int>(nameof(TblContractAcceptanceScopeAllocation.AcceptanceRecordId)) }).Distinct())
        {
            var tracked = ChangeTracker.Entries<TblContractAcceptanceRecord>().SingleOrDefault(x => x.Entity.AcceptanceRecordId == id);
            byte? status;
            if (tracked is not null)
                status = tracked.State == EntityState.Added ? (byte)0 : tracked.OriginalValues.GetValue<byte>(nameof(TblContractAcceptanceRecord.Status));
            else
            {
                var query = TblContractAcceptanceRecords.AsNoTracking().Where(x => x.AcceptanceRecordId == id).Select(x => (byte?)x.Status);
                status = useAsync ? await query.SingleOrDefaultAsync(ct) : query.SingleOrDefault();
            }
            if (status is not 0) throw new InvalidOperationException("Phạm vi nghiệm thu đã chốt/ký/hủy là bất biến.");
        }
        if (ChangeTracker.Entries<TblContractHandoverEvidence>().Any(IsModifiedOrDeleted))
            throw new InvalidOperationException("Bản ký bàn giao không được sửa/xóa.");
    }
}
