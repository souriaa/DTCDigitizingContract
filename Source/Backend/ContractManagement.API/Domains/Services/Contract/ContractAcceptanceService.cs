using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using ContractManagement.API.Common.Enums;
using ContractManagement.API.Common.Exceptions;
using ContractManagement.API.Domains.DTOs.Requests.Contract;
using ContractManagement.API.Domains.DTOs.Responses.Contract;
using ContractManagement.API.Domains.Policies.Contract;
using ContractManagement.Common.Enums;
using ContractManagement.Domains.Interfaces.Contract;
using ContractManagement.Domains.Interfaces.ContractTemplate;
using ContractManagement.Domains.Interfaces.File;
using ContractManagement.Domains.Policies.ContractTemplate;
using ContractManagement.Domains.Policies.Contract;
using ContractManagement.Domains.Services.ContractTemplate;
using ContractManagement.Domains.Services.File;
using ContractManagement.Infrastructure.MultiTenancy.Interfaces;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Domains.Services.Contract;

public sealed class ContractAcceptanceService : IContractAcceptanceService
{
    private const string ArtifactObjectType = "ContractAcceptanceArtifact";
    private const string EvidenceObjectType = "ContractAcceptanceEvidence";
    private readonly DbDtctechContext _db;
    private readonly IContractResourceAuthorizationService _authorization;
    private readonly IContractAuditWriter _audit;
    private readonly IPrivateFileStorage _files;
    private readonly ICurrentTenant _tenant;
    private readonly IContractTemplatePdfRenderer _pdfRenderer;
    private readonly IContractPaymentDueDateService _paymentDueDates;
    private readonly IContractPlaceholderSourceRegistry _placeholderSources;
    private readonly ContractAcceptanceDocumentRenderer _renderer = new();

    public ContractAcceptanceService(
        DbDtctechContext db,
        IContractResourceAuthorizationService authorization,
        IContractAuditWriter audit,
        IPrivateFileStorage files,
        ICurrentTenant tenant,
        IContractTemplatePdfRenderer pdfRenderer,
        IContractPaymentDueDateService paymentDueDates,
        IContractPlaceholderSourceRegistry? placeholderSources = null)
    {
        _db = db;
        _authorization = authorization;
        _audit = audit;
        _files = files;
        _tenant = tenant;
        _pdfRenderer = pdfRenderer;
        _paymentDueDates = paymentDueDates;
        _placeholderSources = placeholderSources
            ?? new ContractPlaceholderSourceRegistry();
    }

    public async Task<IReadOnlyList<ContractAcceptanceTemplateOptionResponse>>
        GetTemplatesAsync(int contractId, int employeeId,
            CancellationToken cancellationToken = default)
    {
        await _authorization.EnsureCanReadAsync(
            contractId, employeeId, cancellationToken);
        return await (
            from template in _db.TblContractTemplates.AsNoTracking()
            join version in _db.TblContractTemplateVersions.AsNoTracking()
                on template.CurrentPublishedVersionId equals
                    version.TemplateVersionId
            where template.IsActive
                && template.DocumentType ==
                    (byte)TemplateDocumentType.AcceptanceRecord
                && version.Status == (byte)TemplateVersionStatus.Published
                && version.DocumentFileId != null
            orderby template.TemplateName, version.VersionNo descending
            select new ContractAcceptanceTemplateOptionResponse
            {
                TemplateId = template.TemplateId,
                TemplateVersionId = version.TemplateVersionId,
                VersionNo = version.VersionNo,
                TemplateCode = template.TemplateCode,
                TemplateName = template.TemplateName,
                LanguageMode = (ContractLanguageMode)template.LanguageMode
            }).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ContractAcceptanceRecordSummaryResponse>>
        ListAsync(int contractId, int employeeId,
            CancellationToken cancellationToken = default)
    {
        await _authorization.EnsureCanReadAsync(
            contractId, employeeId, cancellationToken);
        return await (
            from record in _db.TblContractAcceptanceRecords.AsNoTracking()
            join version in _db.TblContractVersions.AsNoTracking()
                on record.ContractVersionId equals version.VersionId
            where record.ContractId == contractId
            orderby record.CreatedDate descending, record.AcceptanceRecordId descending
            select new ContractAcceptanceRecordSummaryResponse
            {
                AcceptanceRecordId = record.AcceptanceRecordId,
                ContractVersionId = record.ContractVersionId,
                VersionNo = version.VersionNo,
                AcceptanceCode = record.AcceptanceCode,
                AcceptanceDate = record.AcceptanceDate,
                AcceptanceKind = (AcceptanceKind)record.AcceptanceKind,
                Status = (AcceptanceRecordStatus)record.Status,
                PaymentMilestoneIds = _db.TblContractAcceptanceMilestones
                    .Where(x => x.AcceptanceRecordId ==
                        record.AcceptanceRecordId)
                    .Select(x => x.PaymentMilestoneId)
                    .ToList(),
                RowVersion = Convert.ToBase64String(record.RowVersion)
            }).ToListAsync(cancellationToken);
    }

    public async Task<ContractAcceptanceRecordResponse> GetAsync(
        int contractId, int acceptanceRecordId, int employeeId,
        CancellationToken cancellationToken = default)
    {
        await _authorization.EnsureCanReadAsync(
            contractId, employeeId, cancellationToken);
        return await LoadAsync(
            contractId, acceptanceRecordId, cancellationToken);
    }

    public async Task<ContractAcceptanceRecordResponse> CreateAsync(
        int contractId, CreateContractAcceptanceRecordRequest request,
        int employeeId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _authorization.EnsureCanManageAcceptanceAsync(
            contractId, employeeId, cancellationToken);
        ValidateMetadata(request.AcceptanceCode, request.AcceptanceDate,
            request.Location, request.AcceptanceKind);
        return await ExecuteAsync(async () =>
        {
            var contract = await _db.TblContracts.SingleOrDefaultAsync(
                x => x.ContractId == contractId, cancellationToken)
                ?? throw new KeyNotFoundException("Không tìm thấy hợp đồng.");
            Match(contract.RowVersion, request.ContractRowVersion, "Hợp đồng");
            if (contract.CurrentVersionId != request.ContractVersionId)
                throw Rule("AcceptanceSourceChanged",
                    "Version đã ký hiện hành đã thay đổi.",
                    StatusCodes.Status409Conflict);
            var version = await _db.TblContractVersions.SingleOrDefaultAsync(
                x => x.ContractId == contractId
                    && x.VersionId == request.ContractVersionId,
                cancellationToken)
                ?? throw new KeyNotFoundException("Không tìm thấy version hợp đồng.");
            Match(version.RowVersion, request.VersionRowVersion,
                "Version hợp đồng");
            await EnsureSignedSourceAsync(contract, version, cancellationToken);
            await EnsureCanStartNewRecordAsync(
                contractId, version.VersionId, cancellationToken);
            var acceptanceCode = Required(request.AcceptanceCode, 100,
                "Mã biên bản").ToUpperInvariant();
            await EnsureAcceptanceCodeAvailableAsync(contractId,
                acceptanceCode, null, cancellationToken);

            var template = await LoadPublishedTemplateAsync(
                request.TemplateVersionId, cancellationToken);
            var legal = await _db.TblContractVersionLegalSnapshots
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.ContractId == contractId
                    && x.VersionId == version.VersionId, cancellationToken)
                ?? throw Rule("AcceptanceSourceSnapshotMissing",
                    "Version đã ký thiếu relational legal snapshot.",
                    StatusCodes.Status409Conflict);
            var sourceParties = await _db.TblContractVersionPartySnapshots
                .AsNoTracking()
                .Where(x => x.ContractVersionLegalSnapshotId ==
                    legal.ContractVersionLegalSnapshotId)
                .ToListAsync(cancellationToken);
            if (sourceParties.Count != 2)
                throw Rule("AcceptancePartySnapshotMissing",
                    "Version đã ký thiếu snapshot hai bên.",
                    StatusCodes.Status409Conflict);
            var customer = await _db.TblCustomers.AsNoTracking()
                .SingleOrDefaultAsync(x => x.CustomerId == contract.CustomerId,
                    cancellationToken)
                ?? throw new KeyNotFoundException("Không tìm thấy khách hàng.");
            var customerContactName = Required(
                customer.CustomerContactPersonName,
                300,
                "Người liên lạc của khách hàng");
            var customerContactTitle = Optional(
                customer.CustomerContactPersonTitle,
                300);

            var appendices = await ValidateAppendicesAsync(
                contractId, version.VersionId, request.AppendixIds,
                cancellationToken);
            var milestones = await ValidateMilestonesAsync(
                contractId, version.VersionId, request.AcceptanceKind,
                request.PaymentMilestoneIds, null, cancellationToken);
            var sourceSections = await _db.TblContractTemplateTerms
                .AsNoTracking()
                .Where(x => x.TemplateVersionId == request.TemplateVersionId)
                .OrderBy(x => x.DisplayOrder).ThenBy(x => x.TemplateTermId)
                .ToListAsync(cancellationToken);
            if (sourceSections.Count == 0)
                throw Rule("AcceptanceSectionsMissing",
                    "Acceptance template phải có ít nhất một section.");

            var now = DateTime.UtcNow;
            var row = new TblContractAcceptanceRecord
            {
                ContractId = contractId,
                ContractVersionId = version.VersionId,
                TemplateVersionId = template.Version.TemplateVersionId,
                AcceptanceCode = acceptanceCode,
                AcceptanceDate = request.AcceptanceDate.Date,
                Location = Required(request.Location, 500, "Địa điểm"),
                AcceptanceKind = (byte)request.AcceptanceKind,
                Status = (byte)AcceptanceRecordStatus.Draft,
                CreatedEmployeeId = employeeId,
                CreatedDate = now
            };
            EnsureSyntheticRowVersion(row);
            _db.TblContractAcceptanceRecords.Add(row);
            await _db.SaveChangesAsync(cancellationToken);
            _db.TblContractAcceptanceReferences.Add(new()
            {
                AcceptanceRecordId = row.AcceptanceRecordId,
                ReferenceType = (byte)AcceptanceReferenceType.Contract,
                DisplayOrder = 0
            });
            _db.TblContractAcceptanceReferences.AddRange(
                appendices.Select((appendix, index) =>
                    new TblContractAcceptanceReference
                    {
                        AcceptanceRecordId = row.AcceptanceRecordId,
                        ReferenceType = (byte)AcceptanceReferenceType.Appendix,
                        AppendixId = appendix.AppendixId,
                        DisplayOrder = index + 1
                    }));
            _db.TblContractAcceptanceParties.AddRange(
                sourceParties.Select(party =>
                {
                    var partyRole = party.PartyRole == 1
                        ? (byte)AcceptancePartyRole.Tenant
                        : (byte)AcceptancePartyRole.Customer;
                    var isCustomer = partyRole ==
                        (byte)AcceptancePartyRole.Customer;
                    return new TblContractAcceptanceParty
                    {
                        AcceptanceRecordId = row.AcceptanceRecordId,
                        PartyRole = partyRole,
                        LegalName = party.LegalName,
                        Address = party.Address,
                        Phone = party.PhoneNumber,
                        Fax = party.FaxNumber,
                        TaxCode = party.TaxCode,
                        RepresentativeName = isCustomer
                            ? customerContactName
                            : party.RepresentativeName,
                        RepresentativeTitle = isCustomer
                            ? customerContactTitle
                            : party.RepresentativeTitle
                    };
                }));
            var sections = sourceSections.Select(section =>
                    new TblContractAcceptanceSection
                    {
                        AcceptanceRecordId = row.AcceptanceRecordId,
                        SectionCode = section.TermCode,
                        TitleVi = section.TermTitle,
                        TitleEn = section.TermTitleEn,
                        ContentVi = section.TermContent,
                        ContentEn = section.TermContentEn,
                        DisplayOrder = section.DisplayOrder
                    }).ToList();
            sections.ForEach(EnsureSyntheticRowVersion);
            _db.TblContractAcceptanceSections.AddRange(sections);
            _db.TblContractAcceptanceMilestones.AddRange(
                milestones.Select(milestone =>
                    new TblContractAcceptanceMilestone
                    {
                        AcceptanceRecordId = row.AcceptanceRecordId,
                        PaymentMilestoneId = milestone.PaymentMilestoneId
                    }));
            StageAudit(row, employeeId,
                ContractAuditActionTypes.AcceptanceRecordCreated,
                appendices.Count);
            await _db.SaveChangesAsync(cancellationToken);
            return await LoadAsync(contractId, row.AcceptanceRecordId,
                cancellationToken);
        }, cancellationToken);
    }

    public async Task<ContractAcceptanceRecordResponse> UpdateAsync(
        int contractId, int acceptanceRecordId,
        UpdateContractAcceptanceRecordRequest request, int employeeId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _authorization.EnsureCanManageAcceptanceAsync(
            contractId, employeeId, cancellationToken);
        ValidateMetadata(request.AcceptanceCode, request.AcceptanceDate,
            request.Location, request.AcceptanceKind);
        return await ExecuteAsync(async () =>
        {
            var row = await RequireDraftAsync(contractId, acceptanceRecordId,
                request.RowVersion, cancellationToken);
            var acceptanceCode = Required(request.AcceptanceCode, 100,
                "Mã biên bản").ToUpperInvariant();
            await EnsureAcceptanceCodeAvailableAsync(contractId,
                acceptanceCode, acceptanceRecordId, cancellationToken);
            row.AcceptanceCode = acceptanceCode;
            row.AcceptanceDate = request.AcceptanceDate.Date;
            row.Location = Required(request.Location, 500, "Địa điểm");
            row.AcceptanceKind = (byte)request.AcceptanceKind;
            var parties = await _db.TblContractAcceptanceParties
                .Where(x => x.AcceptanceRecordId == acceptanceRecordId)
                .ToListAsync(cancellationToken);
            if (parties.Count != 2)
                throw Rule("AcceptancePartySnapshotMissing",
                    "Biên bản thiếu snapshot hai bên.",
                    StatusCodes.Status409Conflict);
            ApplyParty(parties.Single(x => x.PartyRole ==
                (byte)AcceptancePartyRole.Customer), request.CustomerParty);
            ApplyParty(parties.Single(x => x.PartyRole ==
                (byte)AcceptancePartyRole.Tenant), request.TenantParty);
            Touch(row, employeeId);
            StageAudit(row, employeeId,
                ContractAuditActionTypes.AcceptanceRecordUpdated,
                await CountAppendixReferencesAsync(
                    acceptanceRecordId, cancellationToken));
            await _db.SaveChangesAsync(cancellationToken);
            return await LoadAsync(contractId, acceptanceRecordId,
                cancellationToken);
        }, cancellationToken);
    }

    public async Task<ContractAcceptanceRecordResponse> ReplaceReferencesAsync(
        int contractId, int acceptanceRecordId,
        ReplaceContractAcceptanceReferencesRequest request, int employeeId,
        CancellationToken cancellationToken = default)
    {
        await _authorization.EnsureCanManageAcceptanceAsync(
            contractId, employeeId, cancellationToken);
        return await ExecuteAsync(async () =>
        {
            var row = await RequireDraftAsync(contractId, acceptanceRecordId,
                request.RowVersion, cancellationToken);
            var appendices = await ValidateAppendicesAsync(contractId,
                row.ContractVersionId, request.AppendixIds, cancellationToken);
            var old = await _db.TblContractAcceptanceReferences
                .Where(x => x.AcceptanceRecordId == acceptanceRecordId)
                .ToListAsync(cancellationToken);
            _db.TblContractAcceptanceReferences.RemoveRange(old);
            _db.TblContractAcceptanceReferences.Add(new()
            {
                AcceptanceRecordId = acceptanceRecordId,
                ReferenceType = (byte)AcceptanceReferenceType.Contract,
                DisplayOrder = 0
            });
            _db.TblContractAcceptanceReferences.AddRange(
                appendices.Select((appendix, index) =>
                    new TblContractAcceptanceReference
                    {
                        AcceptanceRecordId = acceptanceRecordId,
                        ReferenceType = (byte)AcceptanceReferenceType.Appendix,
                        AppendixId = appendix.AppendixId,
                        DisplayOrder = index + 1
                    }));
            Touch(row, employeeId);
            StageAudit(row, employeeId,
                ContractAuditActionTypes.AcceptanceRecordUpdated,
                appendices.Count);
            await _db.SaveChangesAsync(cancellationToken);
            return await LoadAsync(contractId, acceptanceRecordId,
                cancellationToken);
        }, cancellationToken);
    }

    public async Task<ContractAcceptanceRecordResponse> ReplaceSectionsAsync(
        int contractId, int acceptanceRecordId,
        ReplaceContractAcceptanceSectionsRequest request, int employeeId,
        CancellationToken cancellationToken = default)
    {
        await _authorization.EnsureCanManageAcceptanceAsync(
            contractId, employeeId, cancellationToken);
        if (request.Sections.Count == 0)
            throw Rule("AcceptanceSectionsMissing",
                "Biên bản phải có ít nhất một section.");
        ValidateSections(request.Sections);
        return await ExecuteAsync(async () =>
        {
            var row = await RequireDraftAsync(contractId, acceptanceRecordId,
                request.RowVersion, cancellationToken);
            var languageMode = await LoadTemplateLanguageModeAsync(
                row.TemplateVersionId, cancellationToken);
            ValidateSectionLanguage(request.Sections, languageMode);
            var old = await _db.TblContractAcceptanceSections
                .Where(x => x.AcceptanceRecordId == acceptanceRecordId)
                .ToListAsync(cancellationToken);
            _db.TblContractAcceptanceSections.RemoveRange(old);
            var sections = request.Sections.Select(
                section => new TblContractAcceptanceSection
                {
                    AcceptanceRecordId = acceptanceRecordId,
                    SectionCode = Required(section.SectionCode, 100,
                        "Mã section").ToUpperInvariant(),
                    TitleVi = Required(section.TitleVi, 1000,
                        "Tiêu đề section"),
                    TitleEn = Optional(section.TitleEn, 1000),
                    ContentVi = NormalizeRichText(section.ContentVi),
                    ContentEn = NormalizeRichText(section.ContentEn),
                    DisplayOrder = section.DisplayOrder
                }).ToList();
            sections.ForEach(EnsureSyntheticRowVersion);
            _db.TblContractAcceptanceSections.AddRange(sections);
            Touch(row, employeeId);
            StageAudit(row, employeeId,
                ContractAuditActionTypes.AcceptanceRecordUpdated,
                await CountAppendixReferencesAsync(
                    acceptanceRecordId, cancellationToken));
            await _db.SaveChangesAsync(cancellationToken);
            return await LoadAsync(contractId, acceptanceRecordId,
                cancellationToken);
        }, cancellationToken);
    }

    public async Task<ContractAcceptanceRecordResponse> ReplaceMilestonesAsync(
        int contractId, int acceptanceRecordId,
        ReplaceContractAcceptanceMilestonesRequest request, int employeeId,
        CancellationToken cancellationToken = default)
    {
        await _authorization.EnsureCanManageAcceptanceAsync(
            contractId, employeeId, cancellationToken);
        return await ExecuteAsync(async () =>
        {
            var row = await RequireDraftAsync(contractId, acceptanceRecordId,
                request.RowVersion, cancellationToken);
            var milestones = await ValidateMilestonesAsync(contractId,
                row.ContractVersionId, (AcceptanceKind)row.AcceptanceKind,
                request.PaymentMilestoneIds, acceptanceRecordId,
                cancellationToken);
            var old = await _db.TblContractAcceptanceMilestones
                .Where(x => x.AcceptanceRecordId == acceptanceRecordId)
                .ToListAsync(cancellationToken);
            _db.TblContractAcceptanceMilestones.RemoveRange(old);
            _db.TblContractAcceptanceMilestones.AddRange(milestones.Select(
                milestone => new TblContractAcceptanceMilestone
                {
                    AcceptanceRecordId = acceptanceRecordId,
                    PaymentMilestoneId = milestone.PaymentMilestoneId
                }));
            Touch(row, employeeId);
            StageAudit(row, employeeId,
                ContractAuditActionTypes.AcceptanceRecordUpdated,
                await CountAppendixReferencesAsync(
                    acceptanceRecordId, cancellationToken));
            await _db.SaveChangesAsync(cancellationToken);
            return await LoadAsync(contractId, acceptanceRecordId,
                cancellationToken);
        }, cancellationToken);
    }

    public async Task<ContractAcceptanceDocumentContent> PreviewAsync(
        int contractId, int acceptanceRecordId, string format, int employeeId,
        CancellationToken cancellationToken = default)
    {
        await _authorization.EnsureCanReadAsync(
            contractId, employeeId, cancellationToken);
        var record = await _db.TblContractAcceptanceRecords.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ContractId == contractId
                && x.AcceptanceRecordId == acceptanceRecordId,
                cancellationToken)
            ?? throw new KeyNotFoundException("Không tìm thấy biên bản nghiệm thu.");
        if ((AcceptanceRecordStatus)record.Status != AcceptanceRecordStatus.Draft)
            throw Rule("AcceptanceNotDraft",
                "Chỉ biên bản Draft mới tạo preview động.",
                StatusCodes.Status409Conflict);
        var docx = await RenderDocxAsync(
            contractId, acceptanceRecordId, cancellationToken);
        return await ToDocumentContentAsync(
            record.AcceptanceCode, format, docx, cancellationToken);
    }

    public async Task<ContractAcceptanceRecordResponse> FinalizeAsync(
        int contractId, int acceptanceRecordId,
        FinalizeContractAcceptanceRecordRequest request, int employeeId,
        CancellationToken cancellationToken = default)
    {
        await _authorization.EnsureCanManageAcceptanceAsync(
            contractId, employeeId, cancellationToken);
        var before = await LoadAsync(
            contractId, acceptanceRecordId, cancellationToken);
        if (before.Status != AcceptanceRecordStatus.Draft)
            throw Rule("AcceptanceNotDraft",
                "Chỉ biên bản Draft mới được finalize.",
                StatusCodes.Status409Conflict);
        if (!string.Equals(before.RowVersion, request.RowVersion,
                StringComparison.Ordinal))
            throw Rule("StaleRowVersion", "Biên bản đã thay đổi.",
                StatusCodes.Status409Conflict);
        ValidateFinalizable(before);
        await EnsureRecordSourceAsync(
            contractId, before.ContractVersionId, cancellationToken);
        var docx = await RenderDocxAsync(
            contractId, acceptanceRecordId, cancellationToken);
        var pdf = await _pdfRenderer.ConvertPreviewToPdfAsync(
            docx, cancellationToken);
        var stored = new List<StoredPrivateFile>();
        try
        {
            stored.Add(await SaveArtifactAsync(acceptanceRecordId,
                $"{SafeFileName(before.AcceptanceCode)}.docx",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                docx, cancellationToken));
            stored.Add(await SaveArtifactAsync(acceptanceRecordId,
                $"{SafeFileName(before.AcceptanceCode)}.pdf",
                "application/pdf", pdf, cancellationToken));
            return await ExecuteAsync(async () =>
            {
                var row = await RequireDraftAsync(contractId,
                    acceptanceRecordId, request.RowVersion, cancellationToken);
                await EnsureRecordSourceAsync(
                    contractId, row.ContractVersionId, cancellationToken);
                if (await _db.TblFileStorages.AnyAsync(x =>
                        x.ObjectType == ArtifactObjectType
                        && x.ObjectId == acceptanceRecordId,
                        cancellationToken))
                    throw Rule("AcceptanceArtifactsExist",
                        "Biên bản đã có artifact chính thức.",
                        StatusCodes.Status409Conflict);
                var now = DateTime.UtcNow;
                var hash = ComputeSnapshotHash(before);
                var docxFile = CreateFile(stored[0], ArtifactObjectType,
                    acceptanceRecordId, employeeId);
                var pdfFile = CreateFile(stored[1], ArtifactObjectType,
                    acceptanceRecordId, employeeId);
                _db.TblFileStorages.AddRange(docxFile, pdfFile);
                row.SnapshotHash = hash;
                row.Status = (byte)AcceptanceRecordStatus.Finalized;
                row.FinalizedAt = now;
                row.FinalizedByEmployeeId = employeeId;
                Touch(row, employeeId, now);
                await _db.SaveChangesAsync(cancellationToken);
                StageAudit(row, employeeId,
                    ContractAuditActionTypes.AcceptanceRecordFinalized,
                    before.References.Count(x =>
                        x.ReferenceType == AcceptanceReferenceType.Appendix),
                    docxFile, pdfFile);
                await _db.SaveChangesAsync(cancellationToken);
                return await LoadAsync(contractId, acceptanceRecordId,
                    cancellationToken);
            }, cancellationToken);
        }
        catch
        {
            _db.ChangeTracker.Clear();
            foreach (var file in stored)
                await _files.DeleteAsync(
                    file.TenantCode, file.StorageKey, cancellationToken);
            throw;
        }
    }

    public async Task<ContractAcceptanceEvidenceResponse>
        UploadSignedEvidenceAsync(int contractId, int acceptanceRecordId,
            UploadContractAcceptanceSignedEvidenceRequest request,
            int employeeId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.File is null || request.File.Length <= 0)
            throw Rule("AcceptanceFileRequired",
                "Vui lòng chọn bản ký biên bản nghiệm thu.");
        await _authorization.EnsureCanManageAcceptanceAsync(
            contractId, employeeId, cancellationToken);
        var stored = await SaveEvidenceAsync(
            contractId, request.File, cancellationToken);
        try
        {
            return await ExecuteAsync(async () =>
            {
                var row = await _db.TblContractAcceptanceRecords
                    .SingleOrDefaultAsync(x => x.ContractId == contractId
                        && x.AcceptanceRecordId == acceptanceRecordId,
                        cancellationToken)
                    ?? throw new KeyNotFoundException(
                        "Không tìm thấy biên bản nghiệm thu.");
                Match(row.RowVersion, request.RowVersion,
                    "Biên bản nghiệm thu");
                AcceptanceRecordPolicy.EnsureCanTransition(
                    (AcceptanceRecordStatus)row.Status,
                    AcceptanceRecordStatus.Signed);
                var contract = await _db.TblContracts.SingleAsync(
                    x => x.ContractId == contractId, cancellationToken);
                var version = await _db.TblContractVersions.SingleAsync(
                    x => x.ContractId == contractId
                        && x.VersionId == row.ContractVersionId,
                    cancellationToken);
                await EnsureSignedSourceAsync(contract, version,
                    cancellationToken);
                var artifactCount = await _db.TblFileStorages.CountAsync(x =>
                    x.ObjectType == ArtifactObjectType
                    && x.ObjectId == acceptanceRecordId
                    && (x.FileType == "docx" || x.FileType == "pdf")
                    && x.Sha256 != null, cancellationToken);
                if (artifactCount != 2 || string.IsNullOrWhiteSpace(row.SnapshotHash))
                    throw Rule("AcceptanceArtifactsMissing",
                        "Biên bản chưa có đủ artifact DOCX/PDF chính thức.",
                        StatusCodes.Status409Conflict);
                if (await _db.TblContractAcceptanceEvidences.AnyAsync(x =>
                        x.AcceptanceRecordId == acceptanceRecordId,
                        cancellationToken))
                    throw Rule("AcceptanceEvidenceExists",
                        "Biên bản đã có bản ký.", StatusCodes.Status409Conflict);

                var file = CreateFile(stored, EvidenceObjectType,
                    contractId, employeeId);
                _db.TblFileStorages.Add(file);
                await _db.SaveChangesAsync(cancellationToken);
                var now = DateTime.UtcNow;
                var evidence = new TblContractAcceptanceEvidence
                {
                    AcceptanceRecordId = acceptanceRecordId,
                    FileId = file.FileId,
                    UploadedByEmployeeId = employeeId,
                    UploadedAt = now
                };
                EnsureSyntheticRowVersion(evidence);
                _db.TblContractAcceptanceEvidences.Add(evidence);
                row.Status = (byte)AcceptanceRecordStatus.Signed;
                row.SignedAt = now;
                row.SignedByEmployeeId = employeeId;
                Touch(row, employeeId, now);
                await ActivateAcceptanceMilestonesAsync(
                    row.AcceptanceRecordId,
                    version.VersionId,
                    (AcceptanceKind)row.AcceptanceKind,
                    now.Date,
                    employeeId,
                    cancellationToken);
                // Materialize the evidence identity before using it as an audit
                // subject. The surrounding execution transaction still keeps
                // evidence, milestone activation and audit atomic.
                await _db.SaveChangesAsync(cancellationToken);
                _audit.StageEmployeeAudits([new(
                    contractId, version.VersionId, employeeId,
                    ContractAuditActionTypes.AcceptanceEvidenceUploaded,
                    ContractAuditResults.Succeeded, now,
                    SubjectType: ContractAuditSubjectTypes.AcceptanceEvidence,
                    SubjectId: evidence.AcceptanceEvidenceId,
                    NewValues: ContractAuditValues.Create(
                        ("AcceptanceEvidenceId", evidence.AcceptanceEvidenceId),
                        ("FileId", file.FileId),
                        ("FileType", file.FileType),
                        ("Sha256", file.Sha256),
                        ("CurrentVersionId", version.VersionId))) ]);
                await _db.SaveChangesAsync(cancellationToken);
                return await LoadEvidenceAsync(
                    evidence.AcceptanceEvidenceId, cancellationToken);
            }, cancellationToken);
        }
        catch
        {
            _db.ChangeTracker.Clear();
            await _files.DeleteAsync(
                stored.TenantCode, stored.StorageKey, cancellationToken);
            throw;
        }
    }

    public async Task<ContractAcceptanceRecordResponse> CancelAsync(
        int contractId, int acceptanceRecordId,
        CancelContractAcceptanceRecordRequest request, int employeeId,
        CancellationToken cancellationToken = default)
    {
        await _authorization.EnsureCanManageAcceptanceAsync(
            contractId, employeeId, cancellationToken);
        var reason = Required(request.Reason, 1000, "Lý do hủy");
        return await ExecuteAsync(async () =>
        {
            var row = await _db.TblContractAcceptanceRecords
                .SingleOrDefaultAsync(x => x.ContractId == contractId
                    && x.AcceptanceRecordId == acceptanceRecordId,
                    cancellationToken)
                ?? throw new KeyNotFoundException(
                    "Không tìm thấy biên bản nghiệm thu.");
            Match(row.RowVersion, request.RowVersion, "Biên bản nghiệm thu");
            AcceptanceRecordPolicy.EnsureCanTransition(
                (AcceptanceRecordStatus)row.Status,
                AcceptanceRecordStatus.Cancelled);
            var now = DateTime.UtcNow;
            row.Status = (byte)AcceptanceRecordStatus.Cancelled;
            row.CancelledAt = now;
            row.CancelledByEmployeeId = employeeId;
            row.CancellationReason = reason;
            Touch(row, employeeId, now);
            StageAudit(row, employeeId,
                ContractAuditActionTypes.AcceptanceRecordCancelled,
                await CountAppendixReferencesAsync(
                    acceptanceRecordId, cancellationToken));
            await _db.SaveChangesAsync(cancellationToken);
            return await LoadAsync(
                contractId, acceptanceRecordId, cancellationToken);
        }, cancellationToken);
    }

    public async Task<ContractAcceptanceDocumentContent> DownloadArtifactAsync(
        int contractId, int acceptanceRecordId, string format, int employeeId,
        CancellationToken cancellationToken = default)
    {
        await _authorization.EnsureCanReadAsync(
            contractId, employeeId, cancellationToken);
        var normalized = NormalizeFormat(format);
        var file = await _db.TblFileStorages.AsNoTracking()
            .SingleOrDefaultAsync(x => x.ObjectType == ArtifactObjectType
                && x.ObjectId == acceptanceRecordId
                && x.FileType == normalized, cancellationToken)
            ?? throw new KeyNotFoundException(
                "Không tìm thấy artifact biên bản nghiệm thu.");
        var recordExists = await _db.TblContractAcceptanceRecords.AsNoTracking()
            .AnyAsync(x => x.ContractId == contractId
                && x.AcceptanceRecordId == acceptanceRecordId,
                cancellationToken);
        if (!recordExists)
            throw new KeyNotFoundException(
                "Không tìm thấy biên bản nghiệm thu.");
        if (string.IsNullOrWhiteSpace(file.StorageKey)
            || string.IsNullOrWhiteSpace(file.TenantCode))
            throw Rule("AcceptanceArtifactInvalid",
                "Artifact không thuộc private storage schema hiện hành.",
                StatusCodes.Status409Conflict);
        await using var stream = await _files.OpenReadAsync(
            file.TenantCode, file.StorageKey, cancellationToken);
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        return new(memory.ToArray(), file.FileName,
            file.ContentType ?? ContentType(normalized));
    }

    private async Task<ContractAcceptanceRecordResponse> LoadAsync(
        int contractId, int acceptanceRecordId, CancellationToken ct)
    {
        var header = await (
            from record in _db.TblContractAcceptanceRecords.AsNoTracking()
            join version in _db.TblContractVersions.AsNoTracking()
                on record.ContractVersionId equals version.VersionId
            join templateVersion in _db.TblContractTemplateVersions.AsNoTracking()
                on record.TemplateVersionId equals templateVersion.TemplateVersionId
            join template in _db.TblContractTemplates.AsNoTracking()
                on templateVersion.TemplateId equals template.TemplateId
            where record.ContractId == contractId
                && record.AcceptanceRecordId == acceptanceRecordId
            select new { record, version.VersionNo, template.TemplateName })
            .SingleOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException(
                "Không tìm thấy biên bản nghiệm thu.");
        var parties = await _db.TblContractAcceptanceParties.AsNoTracking()
            .Where(x => x.AcceptanceRecordId == acceptanceRecordId)
            .OrderBy(x => x.PartyRole).ToListAsync(ct);
        var partyResponses = parties.Select(MapParty).ToList();
        var references = await _db.TblContractAcceptanceReferences.AsNoTracking()
            .Where(x => x.AcceptanceRecordId == acceptanceRecordId)
            .OrderBy(x => x.DisplayOrder).ToListAsync(ct);
        var appendixIds = references.Where(x => x.AppendixId.HasValue)
            .Select(x => x.AppendixId!.Value).ToList();
        var appendices = await _db.TblContractAppendices.AsNoTracking()
            .Where(x => appendixIds.Contains(x.AppendixId))
            .ToDictionaryAsync(x => x.AppendixId, ct);
        var legal = await _db.TblContractVersionLegalSnapshots.AsNoTracking()
            .SingleAsync(x => x.ContractId == contractId
                && x.VersionId == header.record.ContractVersionId, ct);
        var contract = await _db.TblContracts.AsNoTracking()
            .SingleAsync(x => x.ContractId == contractId, ct);
        var finalizedByEmployeeName = header.record.FinalizedByEmployeeId
            .HasValue
            ? await _db.TblEmployees.AsNoTracking()
                .Where(x => x.EmployeeId ==
                    header.record.FinalizedByEmployeeId.Value)
                .Select(x => x.EmployeeFullName)
                .SingleOrDefaultAsync(ct)
            : null;
        var referenceResponses = references.Select(reference =>
            FormatReference(reference, legal, contract.SignDate,
                partyResponses, appendices)).ToList();
        var sections = await _db.TblContractAcceptanceSections.AsNoTracking()
            .Where(x => x.AcceptanceRecordId == acceptanceRecordId)
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.AcceptanceSectionId)
            .Select(x => new ContractAcceptanceSectionResponse
            {
                AcceptanceSectionId = x.AcceptanceSectionId,
                SectionCode = x.SectionCode,
                TitleVi = x.TitleVi,
                TitleEn = x.TitleEn,
                ContentVi = x.ContentVi,
                ContentEn = x.ContentEn,
                DisplayOrder = x.DisplayOrder,
                RowVersion = Convert.ToBase64String(x.RowVersion)
            }).ToListAsync(ct);
        var milestones = await (
            from selected in _db.TblContractAcceptanceMilestones.AsNoTracking()
            join milestone in _db.TblContractPaymentMilestones.AsNoTracking()
                on selected.PaymentMilestoneId equals milestone.PaymentMilestoneId
            where selected.AcceptanceRecordId == acceptanceRecordId
            orderby milestone.DisplayOrder, milestone.PaymentMilestoneId
            select new ContractAcceptanceMilestoneResponse
            {
                AcceptanceMilestoneId = selected.AcceptanceMilestoneId,
                PaymentMilestoneId = milestone.PaymentMilestoneId,
                MilestoneCode = milestone.MilestoneCode,
                TitleVi = milestone.TitleVi,
                TitleEn = milestone.TitleEn
            }).ToListAsync(ct);
        var artifacts = await _db.TblFileStorages.AsNoTracking()
            .Where(x => x.ObjectType == ArtifactObjectType
                && x.ObjectId == acceptanceRecordId)
            .OrderBy(x => x.FileType)
            .Select(x => new ContractAcceptanceArtifactResponse
            {
                FileId = x.FileId,
                Format = x.FileType ?? string.Empty,
                FileName = x.FileName,
                ContentType = x.ContentType ?? string.Empty,
                FileSize = x.FileSize ?? 0,
                Sha256 = x.Sha256 ?? string.Empty
            }).ToListAsync(ct);
        var evidenceId = await _db.TblContractAcceptanceEvidences.AsNoTracking()
            .Where(x => x.AcceptanceRecordId == acceptanceRecordId)
            .Select(x => (int?)x.AcceptanceEvidenceId)
            .SingleOrDefaultAsync(ct);
        return new ContractAcceptanceRecordResponse
        {
            AcceptanceRecordId = acceptanceRecordId,
            ContractId = contractId,
            ContractVersionId = header.record.ContractVersionId,
            VersionNo = header.VersionNo,
            TemplateVersionId = header.record.TemplateVersionId,
            TemplateName = header.TemplateName,
            AcceptanceCode = header.record.AcceptanceCode,
            AcceptanceDate = header.record.AcceptanceDate,
            Location = header.record.Location,
            AcceptanceKind = (AcceptanceKind)header.record.AcceptanceKind,
            Status = (AcceptanceRecordStatus)header.record.Status,
            SnapshotHash = header.record.SnapshotHash,
            FinalizedAt = header.record.FinalizedAt,
            FinalizedByEmployeeId = header.record.FinalizedByEmployeeId,
            FinalizedByEmployeeName = finalizedByEmployeeName,
            SignedAt = header.record.SignedAt,
            CancelledAt = header.record.CancelledAt,
            CancellationReason = header.record.CancellationReason,
            RowVersion = Convert.ToBase64String(header.record.RowVersion),
            References = referenceResponses,
            Parties = partyResponses,
            Sections = sections,
            Milestones = milestones,
            Artifacts = artifacts,
            SignedEvidence = evidenceId.HasValue
                ? await LoadEvidenceAsync(evidenceId.Value, ct) : null
        };
    }

    private async Task<byte[]> RenderDocxAsync(
        int contractId, int acceptanceRecordId, CancellationToken ct)
    {
        var detail = await LoadAsync(contractId, acceptanceRecordId, ct);
        ValidateFinalizable(detail);
        var template = await (
            from version in _db.TblContractTemplateVersions.AsNoTracking()
            join owner in _db.TblContractTemplates.AsNoTracking()
                on version.TemplateId equals owner.TemplateId
            join file in _db.TblFileStorages.AsNoTracking()
                on version.DocumentFileId equals (int?)file.FileId
            where version.TemplateVersionId == detail.TemplateVersionId
                && owner.DocumentType ==
                    (byte)TemplateDocumentType.AcceptanceRecord
            select new { file, owner.LanguageMode }).SingleAsync(ct);
        if (string.IsNullOrWhiteSpace(template.file.StorageKey)
            || string.IsNullOrWhiteSpace(template.file.TenantCode))
            throw Rule("AcceptanceTemplateFileInvalid",
                "Acceptance template không thuộc private storage schema hiện hành.",
                StatusCodes.Status409Conflict);
        await using var stream = await _files.OpenReadAsync(
            template.file.TenantCode, template.file.StorageKey, ct);
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, ct);
        var customValues = await ResolveCustomPlaceholderValuesAsync(
            contractId,
            detail.ContractVersionId,
            detail.TemplateVersionId,
            ct);
        return _renderer.Render(memory.ToArray(), new(
            detail.AcceptanceCode,
            detail.AcceptanceDate,
            detail.Location,
            (ContractLanguageMode)template.LanguageMode,
            detail.References,
            detail.Parties,
            detail.Sections)
        {
            CustomValues = customValues
        });
    }

    private async Task<IReadOnlyDictionary<string, string>>
        ResolveCustomPlaceholderValuesAsync(
            int contractId,
            int contractVersionId,
            int templateVersionId,
            CancellationToken ct)
    {
        var fields = await _db.TblContractTemplateFields.AsNoTracking()
            .Where(field => field.TemplateVersionId == templateVersionId
                && !field.IsSystem)
            .ToListAsync(ct);
        if (fields.Count == 0)
            return new Dictionary<string, string>(StringComparer.Ordinal);

        var contract = await _db.TblContracts.AsNoTracking()
            .SingleAsync(item => item.ContractId == contractId, ct);
        var version = await _db.TblContractVersions.AsNoTracking()
            .SingleAsync(item => item.VersionId == contractVersionId
                && item.ContractId == contractId, ct);
        var customer = await _db.TblCustomers.AsNoTracking()
            .SingleAsync(item => item.CustomerId == contract.CustomerId, ct);
        var tenant = fields.Any(field => field.SourceFieldKey.StartsWith(
                "provider.", StringComparison.Ordinal))
            ? await _db.TblTenantLegalProfiles.AsNoTracking()
                .SingleOrDefaultAsync(ct)
            : null;
        TblEmployee? owner = null;
        TblDepartment? department = null;
        if (fields.Any(field =>
                field.SourceFieldKey.StartsWith(
                    "contract-owner.", StringComparison.Ordinal)
                || field.SourceFieldKey.StartsWith(
                    "department.", StringComparison.Ordinal)))
        {
            owner = await _db.TblEmployees.AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.EmployeeId == contract.EmployeeId, ct);
            if (owner?.DepartmentId is { } departmentId)
            {
                department = await _db.TblDepartments.AsNoTracking()
                    .SingleOrDefaultAsync(
                        item => item.DepartmentId == departmentId, ct);
            }
        }

        var context = new ContractPlaceholderResolveContext(
            contract, version, customer, tenant, owner, department);
        return fields.ToDictionary(
            field => field.PlaceholderKey,
            field =>
            {
                var sourceFieldKey = field.SourceFieldKey;
                var raw = _placeholderSources.Resolve(sourceFieldKey, context);
                return _placeholderSources.Format(
                    sourceFieldKey,
                    raw,
                    field.FormatString,
                    field.DefaultValue);
            },
            StringComparer.Ordinal);
    }

    private async Task<ContractAcceptanceDocumentContent> ToDocumentContentAsync(
        string code, string format, byte[] docx, CancellationToken ct)
    {
        var normalized = NormalizeFormat(format);
        if (normalized == "docx")
            return new(docx, $"{SafeFileName(code)}-preview.docx",
                ContentType(normalized));
        var pdf = await _pdfRenderer.ConvertPreviewToPdfAsync(docx, ct);
        return new(pdf, $"{SafeFileName(code)}-preview.pdf",
            ContentType(normalized));
    }

    private async Task<(TblContractTemplate Template,
        TblContractTemplateVersion Version)> LoadPublishedTemplateAsync(
        int templateVersionId, CancellationToken ct)
    {
        var result = await (
            from version in _db.TblContractTemplateVersions
            join template in _db.TblContractTemplates
                on version.TemplateId equals template.TemplateId
            where version.TemplateVersionId == templateVersionId
                && template.DocumentType ==
                    (byte)TemplateDocumentType.AcceptanceRecord
                && template.IsActive
                && template.CurrentPublishedVersionId == templateVersionId
                && version.Status == (byte)TemplateVersionStatus.Published
                && version.DocumentFileId != null
            select new { template, version }).SingleOrDefaultAsync(ct);
        return result is null
            ? throw Rule("AcceptanceTemplateUnavailable",
                "Acceptance template version không còn khả dụng.",
                StatusCodes.Status409Conflict)
            : (result.template, result.version);
    }

    private async Task EnsureSignedSourceAsync(
        TblContract contract, TblContractVersion version, CancellationToken ct)
    {
        AcceptanceRecordPolicy.EnsureCanCreate(
            contract.Status == (byte)ContractStatus.Signed
                && contract.CurrentVersionId == version.VersionId
                && version.IsLocked,
            contract.SignDate);
        var activeEvidence = await _db.TblContractSignedEvidences.AsNoTracking()
            .AnyAsync(x => x.ContractId == contract.ContractId
                && x.VersionId == version.VersionId
                && x.Status == (byte)SignedEvidenceStatus.Active, ct);
        if (!activeEvidence)
            throw Rule("SignedEvidenceRequired",
                "Hợp đồng chưa có bản ký active.",
                StatusCodes.Status409Conflict);
    }

    private async Task EnsureCanStartNewRecordAsync(
        int contractId, int versionId, CancellationToken ct)
    {
        var records = await _db.TblContractAcceptanceRecords
            .AsNoTracking()
            .Where(x => x.ContractId == contractId
                && x.ContractVersionId == versionId
                && x.Status != (byte)AcceptanceRecordStatus.Cancelled)
            .Select(x => new { x.Status, x.AcceptanceKind })
            .ToListAsync(ct);

        if (records.Any(x => x.Status ==
                (byte)AcceptanceRecordStatus.Draft
            || x.Status == (byte)AcceptanceRecordStatus.Finalized))
            throw Rule("AcceptanceRecordInProgress",
                "Hợp đồng đang có biên bản nghiệm thu Draft hoặc " +
                "đã chốt chờ ký. Hãy hoàn tất hoặc hủy biên bản đó.",
                StatusCodes.Status409Conflict);

        if (records.Any(x => x.Status ==
                (byte)AcceptanceRecordStatus.Signed
            && x.AcceptanceKind == (byte)AcceptanceKind.Final))
            throw Rule("AcceptanceFinalAlreadySigned",
                "Hợp đồng đã có biên bản nghiệm thu toàn bộ " +
                "đã ký nên không thể tạo biên bản mới.",
                StatusCodes.Status409Conflict);
    }

    private async Task<List<TblContractAppendix>> ValidateAppendicesAsync(
        int contractId, int versionId, IReadOnlyCollection<int> ids,
        CancellationToken ct)
    {
        if (ids.Any(id => id <= 0) || ids.Distinct().Count() != ids.Count)
            throw Rule("AcceptanceAppendixSelectionInvalid",
                "Danh sách Appendix không hợp lệ hoặc bị trùng.");
        var rows = await _db.TblContractAppendices.AsNoTracking()
            .Where(x => x.ContractId == contractId && x.VersionId == versionId
                && ids.Contains(x.AppendixId))
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.AppendixId)
            .ToListAsync(ct);
        if (rows.Count != ids.Count)
            throw Rule("AcceptanceAppendixSelectionInvalid",
                "Appendix phải thuộc đúng signed package.");
        return rows;
    }

    private async Task<List<TblContractPaymentMilestone>>
        ValidateMilestonesAsync(int contractId, int versionId,
            AcceptanceKind kind, IReadOnlyCollection<int> ids,
            int? currentRecordId, CancellationToken ct)
    {
        if (!Enum.IsDefined(kind))
            throw Rule("AcceptanceKindInvalid", "Loại nghiệm thu không hợp lệ.");
        if (ids.Any(id => id <= 0) || ids.Distinct().Count() != ids.Count)
            throw Rule("AcceptanceMilestoneSelectionInvalid",
                "Danh sách milestone không hợp lệ hoặc bị trùng.");
        if (kind == AcceptanceKind.Partial && ids.Count == 0)
            throw Rule("AcceptanceMilestoneRequired",
                "Nghiệm thu một phần phải chọn ít nhất một milestone.");
        if (kind == AcceptanceKind.Final && ids.Count > 0)
            throw Rule("AcceptanceMilestoneNotAllowed",
                "Nghiệm thu toàn bộ không chọn milestone riêng lẻ.");
        var rows = await _db.TblContractPaymentMilestones.AsNoTracking()
            .Where(x => x.ContractId == contractId && x.VersionId == versionId
                && ids.Contains(x.PaymentMilestoneId))
            .OrderBy(x => x.DisplayOrder).ToListAsync(ct);
        if (rows.Count != ids.Count)
            throw Rule("AcceptanceMilestoneSelectionInvalid",
                "Milestone phải thuộc đúng signed package.");
        var reused = await (
            from selected in _db.TblContractAcceptanceMilestones.AsNoTracking()
            join record in _db.TblContractAcceptanceRecords.AsNoTracking()
                on selected.AcceptanceRecordId equals record.AcceptanceRecordId
            where ids.Contains(selected.PaymentMilestoneId)
                && record.AcceptanceRecordId != currentRecordId
                && (record.Status == (byte)AcceptanceRecordStatus.Finalized
                    || record.Status == (byte)AcceptanceRecordStatus.Signed)
            select selected.PaymentMilestoneId).AnyAsync(ct);
        if (reused)
            throw Rule("AcceptanceMilestoneAlreadyFinalized",
                "Một milestone đã thuộc biên bản được finalize.",
                StatusCodes.Status409Conflict);
        return rows;
    }

    private async Task<TblContractAcceptanceRecord> RequireDraftAsync(
        int contractId, int acceptanceRecordId, string rowVersion,
        CancellationToken ct)
    {
        var row = await _db.TblContractAcceptanceRecords.SingleOrDefaultAsync(
            x => x.ContractId == contractId
                && x.AcceptanceRecordId == acceptanceRecordId, ct)
            ?? throw new KeyNotFoundException(
                "Không tìm thấy biên bản nghiệm thu.");
        Match(row.RowVersion, rowVersion, "Biên bản nghiệm thu");
        if ((AcceptanceRecordStatus)row.Status != AcceptanceRecordStatus.Draft)
            throw Rule("AcceptanceNotDraft",
                "Chỉ biên bản Draft mới được chỉnh sửa.",
                StatusCodes.Status409Conflict);
        return row;
    }

    private static ContractAcceptancePartyResponse MapParty(
        TblContractAcceptanceParty party) => new()
    {
        AcceptancePartyId = party.AcceptancePartyId,
        PartyRole = (AcceptancePartyRole)party.PartyRole,
        LegalName = party.LegalName,
        Address = party.Address,
        Phone = party.Phone,
        Fax = party.Fax,
        TaxCode = party.TaxCode,
        RepresentativeName = party.RepresentativeName,
        RepresentativeTitle = party.RepresentativeTitle
    };

    private static ContractAcceptanceReferenceResponse FormatReference(
        TblContractAcceptanceReference reference,
        TblContractVersionLegalSnapshot legal,
        DateTime? signDate,
        IReadOnlyList<ContractAcceptancePartyResponse> parties,
        IReadOnlyDictionary<int, TblContractAppendix> appendices)
    {
        var tenant = parties.Single(x =>
            x.PartyRole == AcceptancePartyRole.Tenant);
        var customer = parties.Single(x =>
            x.PartyRole == AcceptancePartyRole.Customer);
        if (!signDate.HasValue)
            throw Rule("ContractSignDateRequired",
                "Hợp đồng thiếu ngày ký nghiệp vụ.",
                StatusCodes.Status409Conflict);
        if ((AcceptanceReferenceType)reference.ReferenceType ==
            AcceptanceReferenceType.Contract)
        {
            return new()
            {
                AcceptanceReferenceId = reference.AcceptanceReferenceId,
                ReferenceType = AcceptanceReferenceType.Contract,
                DisplayOrder = reference.DisplayOrder,
                TextVi = $"Căn cứ Hợp đồng số {legal.ContractCode} ký ngày {signDate.Value:dd/MM/yyyy} giữa {tenant.LegalName} và {customer.LegalName} về việc {legal.ContractName}.",
                TextEn = $"Pursuant to Contract No. {legal.ContractCode} signed on {signDate.Value:dd/MM/yyyy} between {tenant.LegalName} and {customer.LegalName} regarding {legal.ContractNameEn ?? legal.ContractName}."
            };
        }
        var appendix = appendices[reference.AppendixId!.Value];
        return new()
        {
            AcceptanceReferenceId = reference.AcceptanceReferenceId,
            ReferenceType = AcceptanceReferenceType.Appendix,
            AppendixId = appendix.AppendixId,
            DisplayOrder = reference.DisplayOrder,
            TextVi = $"Căn cứ Phụ lục {appendix.AppendixCode} - {appendix.AppendixName} thuộc Hợp đồng số {legal.ContractCode}.",
            TextEn = $"Pursuant to Appendix {appendix.AppendixCode} - {appendix.AppendixNameEn ?? appendix.AppendixName} to Contract No. {legal.ContractCode}."
        };
    }

    private static void ApplyParty(
        TblContractAcceptanceParty target, ContractAcceptancePartyInput input)
    {
        target.LegalName = Required(input.LegalName, 500, "Tên pháp nhân");
        target.Address = Optional(input.Address, 1000);
        target.Phone = Optional(input.Phone, 50);
        target.Fax = Optional(input.Fax, 50);
        target.TaxCode = Optional(input.TaxCode, 50);
        target.RepresentativeName = Required(input.RepresentativeName, 300,
            "Người đại diện");
        target.RepresentativeTitle = Optional(input.RepresentativeTitle, 300);
    }

    private static void ValidateMetadata(string code, DateTime date,
        string location, AcceptanceKind kind)
    {
        _ = Required(code, 100, "Mã biên bản");
        _ = Required(location, 500, "Địa điểm");
        if (date == default)
            throw Rule("AcceptanceDateRequired", "Ngày nghiệm thu là bắt buộc.");
        if (!Enum.IsDefined(kind))
            throw Rule("AcceptanceKindInvalid", "Loại nghiệm thu không hợp lệ.");
    }

    private static void ValidateSections(
        IReadOnlyCollection<ContractAcceptanceSectionInput> sections)
    {
        if (sections.Select(x => x.SectionCode.Trim().ToUpperInvariant())
                .Distinct().Count() != sections.Count
            || sections.Select(x => x.DisplayOrder).Distinct().Count()
                != sections.Count)
            throw Rule("AcceptanceSectionsInvalid",
                "Mã hoặc thứ tự section bị trùng.");
        foreach (var section in sections)
        {
            _ = Required(section.SectionCode, 100, "Mã section");
            _ = Required(section.TitleVi, 1000, "Tiêu đề section");
            _ = NormalizeRichText(section.ContentVi);
            _ = NormalizeRichText(section.ContentEn);
        }
    }

    private static void ValidateSectionLanguage(
        IReadOnlyCollection<ContractAcceptanceSectionInput> sections,
        ContractLanguageMode languageMode)
    {
        if (languageMode != ContractLanguageMode.Bilingual) return;
        if (sections.Any(section => string.IsNullOrWhiteSpace(section.TitleEn)
            || (!string.IsNullOrWhiteSpace(section.ContentVi)
                && string.IsNullOrWhiteSpace(section.ContentEn))))
            throw Rule("AcceptanceSectionsBilingualInvalid",
                "Section song ngữ phải có đủ tiêu đề và nội dung tiếng Anh.");
    }

    private async Task<ContractLanguageMode> LoadTemplateLanguageModeAsync(
        int templateVersionId, CancellationToken ct) =>
        await (
            from version in _db.TblContractTemplateVersions.AsNoTracking()
            join template in _db.TblContractTemplates.AsNoTracking()
                on version.TemplateId equals template.TemplateId
            where version.TemplateVersionId == templateVersionId
            select (ContractLanguageMode)template.LanguageMode).SingleAsync(ct);

    private static void ValidateFinalizable(
        ContractAcceptanceRecordResponse record)
    {
        if (record.References.Count == 0
            || record.References[0].ReferenceType !=
                AcceptanceReferenceType.Contract)
            throw Rule("AcceptanceContractReferenceMissing",
                "Biên bản thiếu căn cứ hợp đồng bắt buộc.");
        if (record.Parties.Count != 2
            || record.Sections.Count == 0)
            throw Rule("AcceptanceSnapshotIncomplete",
                "Biên bản thiếu snapshot hai bên hoặc section.");
        if (record.AcceptanceKind == AcceptanceKind.Partial
            && record.Milestones.Count == 0)
            throw Rule("AcceptanceMilestoneRequired",
                "Nghiệm thu một phần phải chọn milestone.");
        if (record.AcceptanceKind == AcceptanceKind.Final
            && record.Milestones.Count > 0)
            throw Rule("AcceptanceMilestoneNotAllowed",
                "Nghiệm thu toàn bộ không chọn milestone riêng lẻ.");
    }

    private static string ComputeSnapshotHash(
        ContractAcceptanceRecordResponse record)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        void Add(object? value)
        {
            var bytes = Encoding.UTF8.GetBytes(value?.ToString() ?? "<null>");
            hash.AppendData(BitConverter.GetBytes(bytes.Length));
            hash.AppendData(bytes);
        }
        Add(record.ContractId); Add(record.ContractVersionId);
        Add(record.TemplateVersionId); Add(record.AcceptanceCode);
        Add(record.AcceptanceDate.ToString("O")); Add(record.Location);
        Add((byte)record.AcceptanceKind);
        foreach (var reference in record.References.OrderBy(x => x.DisplayOrder))
        { Add((byte)reference.ReferenceType); Add(reference.AppendixId); Add(reference.TextVi); Add(reference.TextEn); }
        foreach (var party in record.Parties.OrderBy(x => x.PartyRole))
        { Add((byte)party.PartyRole); Add(party.LegalName); Add(party.Address); Add(party.Phone); Add(party.Fax); Add(party.TaxCode); Add(party.RepresentativeName); Add(party.RepresentativeTitle); }
        foreach (var section in record.Sections.OrderBy(x => x.DisplayOrder))
        { Add(section.SectionCode); Add(section.TitleVi); Add(section.TitleEn); Add(section.ContentVi); Add(section.ContentEn); Add(section.DisplayOrder); }
        foreach (var milestone in record.Milestones.OrderBy(x => x.PaymentMilestoneId))
            Add(milestone.PaymentMilestoneId);
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private void StageAudit(TblContractAcceptanceRecord row, int employeeId,
        string action, int appendixCount, TblFileStorage? docx = null,
        TblFileStorage? pdf = null)
    {
        var values = new List<(string Key, object? Value)>
        {
            ("AcceptanceRecordId", row.AcceptanceRecordId),
            ("CurrentVersionId", row.ContractVersionId),
            ("TemplateVersionId", row.TemplateVersionId),
            ("AcceptanceKind", row.AcceptanceKind),
            ("AcceptanceDate", row.AcceptanceDate),
            ("AcceptanceStatus", row.Status),
            ("AppendixCount", appendixCount),
            ("SnapshotHash", row.SnapshotHash)
        };
        if (docx is not null && pdf is not null)
        {
            values.Add(("DocxFileId", docx.FileId));
            values.Add(("DocxHash", docx.Sha256));
            values.Add(("PdfFileId", pdf.FileId));
            values.Add(("PdfHash", pdf.Sha256));
            values.Add(("ArtifactCount", 2));
        }
        _audit.StageEmployeeAudits([new(
            row.ContractId, row.ContractVersionId, employeeId,
            action, ContractAuditResults.Succeeded,
            row.UpdatedDate ?? row.CreatedDate,
            SubjectType: ContractAuditSubjectTypes.AcceptanceRecord,
            SubjectId: row.AcceptanceRecordId,
            NewValues: ContractAuditValues.Create(values.ToArray()))]);
    }

    private async Task<int> CountAppendixReferencesAsync(
        int acceptanceRecordId, CancellationToken ct) =>
        await _db.TblContractAcceptanceReferences.CountAsync(x =>
            x.AcceptanceRecordId == acceptanceRecordId
            && x.ReferenceType == (byte)AcceptanceReferenceType.Appendix, ct);

    private async Task ActivateAcceptanceMilestonesAsync(
        int acceptanceRecordId,
        int versionId,
        AcceptanceKind kind,
        DateTime anchorDate,
        int employeeId,
        CancellationToken ct)
    {
        var selectedIds = kind == AcceptanceKind.Partial
            ? await _db.TblContractAcceptanceMilestones.AsNoTracking()
                .Where(x => x.AcceptanceRecordId == acceptanceRecordId)
                .Select(x => x.PaymentMilestoneId)
                .ToListAsync(ct)
            : [];
        var rows = await _db.TblContractPaymentMilestones.Where(x =>
            x.VersionId == versionId
            && x.DueAnchor == (byte)PaymentDueAnchor.AcceptanceCompleted
            && x.AnchorDate == null
            && (kind == AcceptanceKind.Final
                || selectedIds.Contains(x.PaymentMilestoneId))).ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var row in rows)
        {
            row.AnchorDate = anchorDate;
            row.DueDate = _paymentDueDates.Calculate(anchorDate,
                row.DueOffsetDays, (PaymentDayCountMode)row.DayCountMode);
            row.UpdatedEmployeeId = employeeId;
            row.UpdatedDate = now;
        }
        if (rows.Count > 0)
        {
            _audit.StageEmployeeAudits(rows.Select(row => new
                EmployeeContractAuditWriteRequest(
                    row.ContractId,
                    versionId,
                    employeeId,
                    ContractAuditActionTypes.PaymentMilestoneAnchored,
                    ContractAuditResults.Succeeded,
                    now,
                    SubjectType: ContractAuditSubjectTypes.Payment,
                    SubjectId: row.PaymentMilestoneId,
                    NewValues: ContractAuditValues.Create(
                        ("PaymentMilestoneId", row.PaymentMilestoneId),
                        ("CurrentVersionId", versionId),
                        ("AnchorDate", row.AnchorDate),
                        ("DueDate", row.DueDate)))).ToList());
        }
    }

    private async Task<ContractAcceptanceEvidenceResponse> LoadEvidenceAsync(
        int evidenceId, CancellationToken ct)
    {
        var row = await (
            from evidence in _db.TblContractAcceptanceEvidences.AsNoTracking()
            join file in _db.TblFileStorages.AsNoTracking()
                on evidence.FileId equals file.FileId
            join record in _db.TblContractAcceptanceRecords.AsNoTracking()
                on evidence.AcceptanceRecordId equals record.AcceptanceRecordId
            join version in _db.TblContractVersions.AsNoTracking()
                on record.ContractVersionId equals version.VersionId
            join employee in _db.TblEmployees.AsNoTracking()
                on evidence.UploadedByEmployeeId equals employee.EmployeeId
            where evidence.AcceptanceEvidenceId == evidenceId
            select new { evidence, file, record, version.VersionNo,
                employee.EmployeeFullName }).SingleAsync(ct);
        return new()
        {
            AcceptanceEvidenceId = row.evidence.AcceptanceEvidenceId,
            ContractId = row.record.ContractId,
            VersionId = row.record.ContractVersionId,
            VersionNo = row.VersionNo,
            FileId = row.file.FileId,
            FileName = row.file.FileName,
            FileType = row.file.FileType ?? string.Empty,
            ContentType = row.file.ContentType ?? string.Empty,
            FileSize = row.file.FileSize ?? 0,
            Sha256 = row.file.Sha256 ?? string.Empty,
            UploadedByEmployeeId = row.evidence.UploadedByEmployeeId,
            UploadedByEmployeeName = row.EmployeeFullName,
            UploadedAt = row.evidence.UploadedAt,
            RowVersion = Convert.ToBase64String(row.evidence.RowVersion)
        };
    }

    private async Task<StoredPrivateFile> SaveArtifactAsync(
        int recordId, string fileName, string contentType, byte[] content,
        CancellationToken ct)
    {
        var tenant = _tenant.GetRequiredTenant();
        await using var stream = new MemoryStream(content, writable: false);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var signature = extension == ".pdf" ? "%PDF-"u8.ToArray()
            : new byte[] { 0x50, 0x4B };
        return await _files.SaveAsync(new(
            stream, fileName, contentType, content.LongLength,
            tenant.TenantCode, ArtifactObjectType, recordId,
            new PrivateFileUploadPolicy(
                [extension], [contentType], 25 * 1024 * 1024,
                new Dictionary<string, IReadOnlyList<byte[]>>(
                    StringComparer.OrdinalIgnoreCase)
                { [extension] = [signature] })), ct);
    }

    private async Task<StoredPrivateFile> SaveEvidenceAsync(
        int contractId, IFormFile file, CancellationToken ct)
    {
        var tenant = _tenant.GetRequiredTenant();
        await using var content = file.OpenReadStream();
        return await _files.SaveAsync(new(
            content, file.FileName, file.ContentType, file.Length,
            tenant.TenantCode, EvidenceObjectType, contractId,
            PrivateFileUploadPolicies.ContractEvidence()), ct);
    }

    private static TblFileStorage CreateFile(StoredPrivateFile file,
        string objectType, int objectId, int employeeId) => new()
    {
        ObjectType = objectType,
        ObjectId = objectId,
        FileName = file.OriginalFileName,
        FilePath = string.Empty,
        StorageKey = file.StorageKey,
        ContentType = file.ContentType,
        Sha256 = file.Sha256,
        TenantCode = file.TenantCode,
        FileType = Path.GetExtension(file.OriginalFileName)
            .TrimStart('.').ToLowerInvariant(),
        FileSize = file.FileSize,
        UploadedByUserId = employeeId,
        UploadedDate = file.CreatedAt
    };

    private async Task EnsureAcceptanceCodeAvailableAsync(int contractId,
        string acceptanceCode, int? currentRecordId, CancellationToken ct)
    {
        var exists = await _db.TblContractAcceptanceRecords.AsNoTracking()
            .AnyAsync(record => record.ContractId == contractId
                && record.AcceptanceCode == acceptanceCode
                && record.AcceptanceRecordId != currentRecordId, ct);
        if (exists) throw DuplicateAcceptanceCode();
    }

    private static BusinessRuleException DuplicateAcceptanceCode() =>
        Rule("AcceptanceCodeAlreadyExists",
            "Mã biên bản đã tồn tại trong hợp đồng này. Vui lòng nhập mã khác.",
            StatusCodes.Status409Conflict);

    private static bool IsAcceptanceCodeUniqueViolation(Exception exception) =>
        exception is DbUpdateException
        {
            InnerException: SqlException { Number: 2601 or 2627 } sqlException
        } && sqlException.Message.Contains(
            "IX_tbl_ContractAcceptanceRecord_ContractId_AcceptanceCode",
            StringComparison.OrdinalIgnoreCase);

    private async Task<T> ExecuteAsync<T>(Func<Task<T>> action,
        CancellationToken ct)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                var result = await action();
                await transaction.CommitAsync(ct);
                return result;
            }
            catch (Exception exception)
            {
                await transaction.RollbackAsync(ct);
                _db.ChangeTracker.Clear();
                if (IsAcceptanceCodeUniqueViolation(exception))
                    throw DuplicateAcceptanceCode();
                throw;
            }
        });
    }

    private void Touch(TblContractAcceptanceRecord row,
        int employeeId, DateTime? now = null)
    {
        row.UpdatedEmployeeId = employeeId;
        row.UpdatedDate = now ?? DateTime.UtcNow;
        if (UsesInMemoryProvider())
            row.RowVersion = Guid.NewGuid().ToByteArray();
    }

    private async Task EnsureRecordSourceAsync(
        int contractId, int versionId, CancellationToken ct)
    {
        var contract = await _db.TblContracts.SingleOrDefaultAsync(
            x => x.ContractId == contractId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy hợp đồng.");
        if (contract.CurrentVersionId != versionId)
            throw Rule("AcceptanceSourceChanged",
                "Version đã ký hiện hành đã thay đổi.",
                StatusCodes.Status409Conflict);
        var version = await _db.TblContractVersions.SingleOrDefaultAsync(
            x => x.ContractId == contractId && x.VersionId == versionId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy version hợp đồng.");
        await EnsureSignedSourceAsync(contract, version, ct);
    }

    private void EnsureSyntheticRowVersion(TblContractAcceptanceRecord row)
    {
        if (UsesInMemoryProvider() &&
            (row.RowVersion is null || row.RowVersion.Length == 0))
            row.RowVersion = Guid.NewGuid().ToByteArray();
    }

    private void EnsureSyntheticRowVersion(TblContractAcceptanceSection row)
    {
        if (UsesInMemoryProvider() &&
            (row.RowVersion is null || row.RowVersion.Length == 0))
            row.RowVersion = Guid.NewGuid().ToByteArray();
    }

    private void EnsureSyntheticRowVersion(TblContractAcceptanceEvidence row)
    {
        if (UsesInMemoryProvider() &&
            (row.RowVersion is null || row.RowVersion.Length == 0))
            row.RowVersion = Guid.NewGuid().ToByteArray();
    }

    private bool UsesInMemoryProvider() => string.Equals(
        _db.Database.ProviderName,
        "Microsoft.EntityFrameworkCore.InMemory",
        StringComparison.Ordinal);

    private static string NormalizeRichText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null!;
        var normalized = value.Trim();
        if (!ContractTermRichText.TryParse(normalized, out _))
            throw Rule("AcceptanceRichTextInvalid",
                "Nội dung section không đúng rich text schema hiện hành.");
        return normalized;
    }

    private static string Required(string? value, int max, string label)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw Rule("RequiredField", $"{label} là bắt buộc.");
        if (normalized.Length > max)
            throw Rule("FieldTooLong",
                $"{label} không được vượt quá {max} ký tự.");
        return normalized;
    }

    private static string? Optional(string? value, int max)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return null;
        if (normalized.Length > max)
            throw Rule("FieldTooLong",
                $"Giá trị không được vượt quá {max} ký tự.");
        return normalized;
    }

    private static void Match(byte[] current, string encoded, string name)
    {
        byte[] expected;
        try { expected = Convert.FromBase64String(encoded); }
        catch { throw Rule("StaleRowVersion", "RowVersion không hợp lệ.",
            StatusCodes.Status409Conflict); }
        if (expected.Length == 0 || !current.AsSpan().SequenceEqual(expected))
            throw new DbUpdateConcurrencyException($"{name} đã thay đổi.");
    }

    private static string NormalizeFormat(string format)
    {
        var normalized = format.Trim().ToLowerInvariant();
        return normalized is "docx" or "pdf" ? normalized
            : throw Rule("AcceptanceFormatInvalid",
                "Format chỉ hỗ trợ docx hoặc pdf.");
    }

    private static string ContentType(string format) => format == "pdf"
        ? "application/pdf"
        : "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(value.Select(character =>
            invalid.Contains(character) ? '-' : character).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "acceptance-record" : safe;
    }

    private static BusinessRuleException Rule(string code, string message,
        int status = StatusCodes.Status400BadRequest) =>
        new(status, code, message);
}
