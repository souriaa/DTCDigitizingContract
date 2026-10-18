using System.ComponentModel.DataAnnotations;
using ContractManagement.API.Common.Enums;
using ContractManagement.API.Domains.DTOs.Requests.Contract;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Domains.Services.Contract;

public partial class ContractService
{
    private static bool HasPaymentDraftChanges(
        IEnumerable<TblContractPaymentMilestone> existing,
        List<ContractPaymentMilestoneDraftRequest> requested)
    {
        var current = existing.OrderBy(x => x.DisplayOrder).ToList();
        return current.Count != requested.Count || current.Where((row, i) => {
            var next = requested[i];
            return row.PaymentMilestoneId != next.PaymentMilestoneId ||
                row.TitleVi != next.TitleVi.Trim() || row.TitleEn != NormalizeOptional(next.TitleEn) ||
                row.PaymentPercent != next.PaymentPercent || row.DueAnchor != (byte)next.DueAnchor ||
                row.DueOffsetDays != next.DueOffsetDays || row.DayCountMode != (byte)next.DayCountMode ||
                row.ConditionVi != NormalizeOptional(next.ConditionVi) || row.ConditionEn != NormalizeOptional(next.ConditionEn) ||
                row.DueAnchor == (byte)PaymentDueAnchor.ManualDate && row.AnchorDate != next.AnchorDate?.Date;
        }).Any();
    }

    private static void ValidatePaymentDraft(
        IEnumerable<(ContractTermKind Kind, List<ContractPaymentMilestoneDraftRequest> Rows)> terms,
        bool bilingual)
    {
        var groups = terms.ToList();
        if (groups.Any(x => !Enum.IsDefined(x.Kind)) ||
            groups.Count(x => x.Kind == ContractTermKind.Payment) > 1)
            throw new ArgumentException("Hợp đồng chỉ được có một điều khoản thanh toán và loại điều khoản phải hợp lệ.");
        foreach (var (kind, rows) in groups)
        {
            if (rows is null) throw new ArgumentException("Danh sách đợt thanh toán không hợp lệ.");
            if (kind != ContractTermKind.Payment && rows.Count > 0)
                throw new ArgumentException("Chỉ điều khoản thanh toán mới được có các đợt thanh toán.");
            // Cho phép lưu kế hoạch đang soạn; tổng 100% được bắt buộc khi gửi duyệt.
            if (rows.Sum(x => x.PaymentPercent) > 100m)
                throw new ArgumentException("Tổng tỷ lệ thanh toán không được vượt quá 100%.");
            if (rows.GroupBy(x => x.MilestoneCode?.Trim(), StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
                throw new ArgumentException("Mã đợt thanh toán bị trùng.");
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var errors = new List<ValidationResult>();
                if (!Validator.TryValidateObject(row, new ValidationContext(row), errors, true) ||
                    string.IsNullOrWhiteSpace(row.MilestoneCode) || string.IsNullOrWhiteSpace(row.TitleVi) ||
                    decimal.Round(row.PaymentPercent, 4) != row.PaymentPercent)
                    throw new ArgumentException("Cấu hình đợt thanh toán không hợp lệ: " + string.Join(" ", errors.Select(x => x.ErrorMessage)));
                if (i == 0 && row.DueAnchor == PaymentDueAnchor.PreviousMilestonePaid)
                    throw new ArgumentException("Đợt đầu tiên không được phụ thuộc thanh toán đợt trước.");
                if (bilingual && (string.IsNullOrWhiteSpace(row.TitleEn) ||
                    !string.IsNullOrWhiteSpace(row.ConditionVi) && string.IsNullOrWhiteSpace(row.ConditionEn)))
                    throw new ArgumentException("Đợt thanh toán song ngữ phải có tên và điều kiện tiếng Anh tương ứng.");
            }
        }
    }

    private async Task EnsurePaymentDraftWritableAsync(List<TblContractPaymentMilestone> existing)
    {
        var ids = existing.Select(x => x.PaymentMilestoneId).ToList();
        if (ids.Count > 0 && (existing.Any(x => x.PaymentStatus != (byte)ContractPaymentMilestoneStatus.Unpaid) ||
            await _dbContext.TblContractAcceptanceMilestones.AnyAsync(x => ids.Contains(x.PaymentMilestoneId)) ||
            await _dbContext.TblContractPaymentLedgers.AnyAsync(x => x.PaymentMilestoneId.HasValue && ids.Contains(x.PaymentMilestoneId.Value))))
            throw new InvalidOperationException("Không thể sửa kế hoạch đã phát sinh thanh toán hoặc nghiệm thu.");
    }

    private async Task SavePaymentDraftAsync(
        int contractId, int versionId, List<TblContractTerm> terms,
        List<(string TermCode, List<ContractPaymentMilestoneDraftRequest> Rows)> drafts,
        List<TblContractPaymentMilestone> existing, decimal total, string currency,
        DateTime? effectiveDate, int employeeId, DateTime now, bool creating = false, bool paymentHistoryChecked = false)
    {
        var rows = drafts.SelectMany(x => x.Rows.Select(row => (x.TermCode, Row: row))).ToList();
        var ids = rows.Where(x => x.Row.PaymentMilestoneId.HasValue).Select(x => x.Row.PaymentMilestoneId!.Value).ToList();
        if (ids.Distinct().Count() != ids.Count || creating && ids.Count > 0 ||
            ids.Any(id => existing.All(x => x.PaymentMilestoneId != id)))
            throw new ArgumentException("Đợt thanh toán không thuộc phiên bản hợp đồng hiện tại hoặc bị lặp.");

        // Không thay đổi quan hệ/định nghĩa các đợt đã tham gia nghiệp vụ thực tế.
        if (!paymentHistoryChecked) await EnsurePaymentDraftWritableAsync(existing);

        foreach (var (_, row) in rows.Where(x => x.Row.PaymentMilestoneId.HasValue))
        {
            var source = existing.Single(x => x.PaymentMilestoneId == row.PaymentMilestoneId);
            EnsureRowVersionMatches(source.RowVersion, DecodeRowVersion(row.RowVersion, "PaymentMilestones.RowVersion"), "Đợt thanh toán");
            if (!string.Equals(source.MilestoneCode, row.MilestoneCode.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Không được đổi mã đợt thanh toán đã lưu.");
            _dbContext.Entry(source).Property(x => x.RowVersion).OriginalValue = source.RowVersion;
        }
        var removed = existing.Where(x => !ids.Contains(x.PaymentMilestoneId)).ToList();
        _dbContext.TblContractPaymentMilestones.RemoveRange(removed);
        // Xóa trước khi thêm để cho phép dùng lại mã của đợt đã xóa, trong cùng transaction.
        if (removed.Count > 0) await _dbContext.SaveChangesAsync();
        var sourceIds = rows.Where(x => !x.Row.PaymentMilestoneId.HasValue && x.Row.SourceTemplatePaymentMilestoneId.HasValue)
            .Select(x => x.Row.SourceTemplatePaymentMilestoneId!.Value).Distinct().ToList();
        var sources = await _dbContext.TblContractTemplatePaymentMilestones.AsNoTracking()
            .Where(x => sourceIds.Contains(x.TemplatePaymentMilestoneId))
            .ToDictionaryAsync(x => x.TemplatePaymentMilestoneId, x => x.TemplateTermId);
        decimal allocated = 0;
        var balanced = rows.Sum(x => x.Row.PaymentPercent) == 100m;
        for (var i = 0; i < rows.Count; i++)
        {
            var (termCode, row) = rows[i];
            var term = terms.Single(x => string.Equals(x.TermCode, termCode, StringComparison.OrdinalIgnoreCase));
            var milestone = row.PaymentMilestoneId is { } id
                ? existing.Single(x => x.PaymentMilestoneId == id)
                : new TblContractPaymentMilestone { ContractId = contractId, VersionId = versionId,
                    CreatedEmployeeId = employeeId, CreatedDate = now };
            if (!row.PaymentMilestoneId.HasValue) _dbContext.TblContractPaymentMilestones.Add(milestone);
            milestone.TermId = term.TermId;
            // Provenance của bản đã lưu không được đổi bởi client.
            if (!row.PaymentMilestoneId.HasValue)
            {
                if (row.SourceTemplatePaymentMilestoneId is { } sourceId &&
                    (!sources.TryGetValue(sourceId, out var sourceTermId) || sourceTermId != term.SourceTemplateTermId))
                    throw new ArgumentException("Đợt thanh toán nguồn không thuộc điều khoản mẫu đã chọn.");
                milestone.SourceTemplatePaymentMilestoneId = row.SourceTemplatePaymentMilestoneId;
            }
            milestone.MilestoneCode = row.MilestoneCode.Trim().ToUpperInvariant();
            milestone.TitleVi = row.TitleVi.Trim();
            milestone.TitleEn = NormalizeOptional(row.TitleEn);
            milestone.PaymentPercent = row.PaymentPercent;
            milestone.DueAnchor = (byte)row.DueAnchor;
            milestone.DueOffsetDays = row.DueOffsetDays;
            milestone.DayCountMode = (byte)row.DayCountMode;
            milestone.ConditionVi = NormalizeOptional(row.ConditionVi);
            milestone.ConditionEn = NormalizeOptional(row.ConditionEn);
            milestone.DisplayOrder = i + 1;
            var requestedAmount = balanced && i == rows.Count - 1
                ? total - allocated : RoundMoney(total * row.PaymentPercent / 100m, currency);
            milestone.Amount = Math.Max(0m, Math.Min(requestedAmount, total - allocated));
            allocated += milestone.Amount;
            milestone.AnchorDate = row.DueAnchor switch {
                PaymentDueAnchor.ContractEffectiveDate => effectiveDate?.Date,
                PaymentDueAnchor.ManualDate => row.AnchorDate?.Date,
                _ => null };
            milestone.DueDate = milestone.AnchorDate is { } anchor
                ? _paymentDueDates.Calculate(anchor, row.DueOffsetDays, row.DayCountMode) : null;
            milestone.UpdatedEmployeeId = employeeId;
            milestone.UpdatedDate = now;
        }
    }
}
