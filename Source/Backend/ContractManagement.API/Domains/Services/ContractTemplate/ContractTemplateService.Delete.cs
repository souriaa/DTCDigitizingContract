using ContractManagement.API.Common.Exceptions;
using ContractManagement.API.Domains.DTOs.Requests.ContractTemplate;
using ContractManagement.Common.Enums;
using ContractManagement.Domains.Interfaces.ContractTemplate;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Domains.Services.ContractTemplate;

public sealed partial class ContractTemplateService
{
    public async Task DeleteAsync(int templateId, DeleteContractTemplateRequest request,
        int employeeId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureAdminOfficerAsync(employeeId, cancellationToken);
        var templateRv = DecodeRowVersion(request.RowVersion, nameof(request.RowVersion));
        if (request.Versions.Count == 0 || request.Versions.Any(x => x.TemplateVersionId <= 0)
            || request.Versions.Select(x => x.TemplateVersionId).Distinct().Count() != request.Versions.Count)
            throw new ArgumentException("Cần gửi đầy đủ phiên bản của mẫu, không trùng phiên bản.");
        var expectedVersions = request.Versions.ToDictionary(x => x.TemplateVersionId,
            x => DecodeRowVersion(x.RowVersion, "Versions.RowVersion"));

        // Range locks also prevent a concurrent publish or new version from escaping the checks.
        var fileIds = await ExecuteInTransactionAsync(async () =>
        {
            var template = await GetTemplateAsync(templateId, cancellationToken);
            EnsureRowVersionMatches(template.RowVersion, templateRv, "Mẫu tài liệu");
            var versions = await _dbContext.TblContractTemplateVersions
                .Where(x => x.TemplateId == templateId).ToListAsync(cancellationToken);
            if (!expectedVersions.Keys.ToHashSet().SetEquals(versions.Select(x => x.TemplateVersionId)))
                throw new DbUpdateConcurrencyException("Danh sách phiên bản đã thay đổi. Vui lòng tải lại mẫu.");
            foreach (var version in versions)
                EnsureRowVersionMatches(version.RowVersion, expectedVersions[version.TemplateVersionId], "Phiên bản mẫu");
            if (template.CurrentPublishedVersionId.HasValue || versions.Any(x =>
                    x.Status != (byte)TemplateVersionStatus.Draft || x.PublishedDate.HasValue
                    || x.PublishedByEmployeeId.HasValue || x.PublishedPreviewPdfFileId.HasValue))
                throw new BusinessRuleException(StatusCodes.Status409Conflict, "TemplateAlreadyPublished",
                    "Không thể xóa mẫu đã từng phát hành. Chỉ mẫu chưa phát hành, chưa được sử dụng mới được xóa.");

            var ids = versions.Select(x => x.TemplateVersionId).ToArray();
            var terms = await _dbContext.TblContractTemplateTerms.Where(x => ids.Contains(x.TemplateVersionId)).ToListAsync(cancellationToken);
            var appendices = await _dbContext.TblContractTemplateAppendices.Where(x => ids.Contains(x.TemplateVersionId)).ToListAsync(cancellationToken);
            var appendixIds = appendices.Select(x => x.TemplateAppendixId).ToArray();
            var appendixTerms = await _dbContext.TblContractTemplateAppendixTerms.Where(x => appendixIds.Contains(x.TemplateAppendixId)).ToListAsync(cancellationToken);
            var milestones = await _dbContext.TblContractTemplatePaymentMilestones.Where(x => ids.Contains(x.TemplateVersionId)).ToListAsync(cancellationToken);
            var bases = await _dbContext.TblContractTemplateLegalBases.Where(x => ids.Contains(x.TemplateVersionId)).ToListAsync(cancellationToken);
            var termIds = terms.Select(x => x.TemplateTermId).ToArray();
            var appendixTermIds = appendixTerms.Select(x => x.TemplateAppendixTermId).ToArray();
            var milestoneIds = milestones.Select(x => x.TemplatePaymentMilestoneId).ToArray();
            var basisIds = bases.Select(x => x.TemplateLegalBasisId).ToArray();
            var inUse = await _dbContext.TblContracts.AnyAsync(x => ids.Contains(x.TemplateVersionId), cancellationToken)
                || await _dbContext.TblContractVersions.AnyAsync(x => ids.Contains(x.TemplateVersionId), cancellationToken)
                || await _dbContext.TblContractVersionLegalSnapshots.AnyAsync(x => ids.Contains(x.TemplateVersionId), cancellationToken)
                || await _dbContext.TblContractVersionPlaceholderValues.AnyAsync(x => ids.Contains(x.TemplateVersionId), cancellationToken)
                || await _dbContext.TblContractAcceptanceRecords.AnyAsync(x => ids.Contains(x.TemplateVersionId), cancellationToken)
                || await _dbContext.TblContractHandoverRecords.AnyAsync(x => ids.Contains(x.TemplateVersionId), cancellationToken)
                || await _dbContext.TblContractTerms.AnyAsync(x => x.SourceTemplateTermId.HasValue && termIds.Contains(x.SourceTemplateTermId.Value), cancellationToken)
                || await _dbContext.TblContractAppendices.AnyAsync(x => x.SourceTemplateAppendixId.HasValue && appendixIds.Contains(x.SourceTemplateAppendixId.Value), cancellationToken)
                || await _dbContext.TblContractAppendixTerms.AnyAsync(x => x.SourceTemplateAppendixTermId.HasValue && appendixTermIds.Contains(x.SourceTemplateAppendixTermId.Value), cancellationToken)
                || await _dbContext.TblContractPaymentMilestones.AnyAsync(x => x.SourceTemplatePaymentMilestoneId.HasValue && milestoneIds.Contains(x.SourceTemplatePaymentMilestoneId.Value), cancellationToken)
                || await _dbContext.TblContractVersionPaymentMilestoneSnapshots.AnyAsync(x => x.SourceTemplatePaymentMilestoneId.HasValue && milestoneIds.Contains(x.SourceTemplatePaymentMilestoneId.Value), cancellationToken)
                || await _dbContext.TblContractLegalBases.AnyAsync(x => x.SourceTemplateLegalBasisId.HasValue && basisIds.Contains(x.SourceTemplateLegalBasisId.Value), cancellationToken);
            if (inUse)
                throw new BusinessRuleException(StatusCodes.Status409Conflict, "TemplateInUse",
                    "Không thể xóa mẫu đang được hợp đồng hoặc biên bản tham chiếu, kể cả bản nháp và bản đã hủy.");

            var ownedFiles = await _dbContext.TblFileStorages.AsNoTracking().Where(x => ids.Contains(x.ObjectId)
                && (x.ObjectType == ContractTemplateVersionObjectType || x.ObjectType == ContractTemplatePreviewObjectType
                    || x.ObjectType == ContractTemplatePublishedPreviewPdfObjectType)).Select(x => x.FileId).ToListAsync(cancellationToken);
            var referencedFiles = versions.SelectMany(x => new[] { x.DocumentFileId, x.PreviewFileId }).Where(x => x.HasValue).Select(x => x!.Value);
            if (referencedFiles.Except(ownedFiles).Any())
                throw new BusinessRuleException(StatusCodes.Status409Conflict, "TemplateFileOwnershipInvalid",
                    "Tệp của mẫu không khớp quyền sở hữu. Không thể xóa mẫu để tránh xóa nhầm tài liệu.");
            if (ownedFiles.Count > 0) EnsureDocumentStorageIsConfigured();

            // Audit is append-only: keep previous events and record deletion before removing the aggregate.
            var now = DateTime.UtcNow;
            _templateAuditWriter?.StageAudits(versions.Select(x => new ContractTemplateAuditWriteRequest(
                templateId, x.TemplateVersionId, employeeId, ContractTemplateAuditActionTypes.TemplateDeleted,
                ContractTemplateAuditResults.Succeeded, now)).ToArray());
            _dbContext.TblContractTemplateAppendixTerms.RemoveRange(appendixTerms);
            _dbContext.TblContractTemplateAppendices.RemoveRange(appendices);
            _dbContext.TblContractTemplatePaymentMilestones.RemoveRange(milestones);
            _dbContext.TblContractTemplateTerms.RemoveRange(terms);
            _dbContext.TblContractTemplateLegalBases.RemoveRange(bases);
            _dbContext.TblContractTemplateFields.RemoveRange(await _dbContext.TblContractTemplateFields.Where(x => ids.Contains(x.TemplateVersionId)).ToListAsync(cancellationToken));
            _dbContext.TblContractTemplateItemTableColumnLayouts.RemoveRange(await _dbContext.TblContractTemplateItemTableColumnLayouts.Where(x => ids.Contains(x.TemplateVersionId)).ToListAsync(cancellationToken));
            _dbContext.TblContractTemplateVersions.RemoveRange(versions);
            _dbContext.TblContractTemplates.Remove(template);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ownedFiles;
        }, cancellationToken, System.Data.IsolationLevel.Serializable);

        // Storage is not transactional. Only remove owned files after SQL deletion has committed.
        foreach (var fileId in fileIds)
        {
            try { await _fileStorageService!.DeleteAsync(fileId); }
            catch (Exception exception)
            {
                _logger?.LogError(exception, "Deleted template {TemplateId} left artifact {FileId} to clean up.", templateId, fileId);
            }
        }
    }
}
