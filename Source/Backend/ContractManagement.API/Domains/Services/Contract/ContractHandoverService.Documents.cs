using ContractManagement.API.Common.Enums;
using ContractManagement.API.Domains.DTOs.Requests.Contract;
using ContractManagement.API.Domains.DTOs.Responses.Contract;
using ContractManagement.Common.Enums;
using ContractManagement.Domains.Policies.ContractTemplate;
using ContractManagement.Domains.Services.ContractTemplate;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
namespace ContractManagement.Domains.Services.Contract;
public sealed partial class ContractHandoverService
{
    private sealed record Template(TblContractTemplate Definition, TblContractTemplateVersion Version, TblFileStorage File);
    private async Task<Template> TemplateAsync(Source source, int templateVersionId, bool forNew, CancellationToken ct)
    {
        var value = await (from v in db.TblContractTemplateVersions.AsNoTracking()
            join t in db.TblContractTemplates.AsNoTracking() on v.TemplateId equals t.TemplateId
            join f in db.TblFileStorages.AsNoTracking() on v.DocumentFileId equals (int?)f.FileId
            where v.TemplateVersionId == templateVersionId && t.DocumentType == (byte)TemplateDocumentType.HandoverRecord
                && v.ContractDirection == source.Legal.ContractDirection && t.LanguageMode == source.Legal.LanguageMode
                && v.ValidationStatus == (byte)TemplateValidationStatus.Valid
            select new { v, t, f }).SingleOrDefaultAsync(ct);
        if (value is null || (forNew ? !value.t.IsActive || value.t.CurrentPublishedVersionId != templateVersionId || value.v.Status != (byte)TemplateVersionStatus.Published
            : value.v.Status is not ((byte)TemplateVersionStatus.Published) and not ((byte)TemplateVersionStatus.Retired)) ||
            value.f.ObjectType != "ContractTemplateVersion" || value.f.ObjectId != templateVersionId)
            throw Rule("HandoverTemplateMismatch", "Mẫu bàn giao phải đã phát hành, cùng chiều Mua/Bán, cùng ngôn ngữ và đúng DOCX nguồn.");
        return new(value.t, value.v, value.f);
    }
    public async Task<HandoverDetailResponse> GetAsync(int contractId, int recordId, int employeeId, CancellationToken ct = default)
    {
        await authorization.EnsureCanReadAsync(contractId, employeeId, ct);
        var row = await RecordAsync(contractId, recordId, ct);
        var source = await SourceAsync(contractId, row.ContractVersionId, false, ct);
        var templateName = await (from v in db.TblContractTemplateVersions.AsNoTracking()
            join t in db.TblContractTemplates.AsNoTracking() on v.TemplateId equals t.TemplateId
            where v.TemplateVersionId == row.TemplateVersionId select new { t.TemplateName, v.VersionNo }).SingleAsync(ct);
        var parties = await db.TblContractHandoverParties.AsNoTracking().Where(x => x.HandoverRecordId == recordId).OrderBy(x => x.SubjectKind).ToListAsync(ct);
        var sectionRows = await db.TblContractHandoverSections.AsNoTracking().Where(x => x.HandoverRecordId == recordId).OrderBy(x => x.DisplayOrder).ToListAsync(ct);
        var referenceRows = await db.TblContractHandoverReferences.AsNoTracking().Where(x => x.HandoverRecordId == recordId).OrderBy(x => x.DisplayOrder).ToListAsync(ct);
        var appendixIds = referenceRows.Where(x => x.AppendixId.HasValue).Select(x => x.AppendixId!.Value).ToArray();
        var appendices = await db.TblContractAppendices.AsNoTracking().Where(x => x.ContractId == contractId && x.VersionId == row.ContractVersionId && appendixIds.Contains(x.AppendixId)).ToDictionaryAsync(x => x.AppendixId, ct);
        if (appendices.Count != appendixIds.Length || referenceRows.Count == 0 || referenceRows[0].AppendixId.HasValue)
            throw Rule("HandoverReferenceMismatch", "Căn cứ không thuộc phiên bản hợp đồng.");
        var references = referenceRows.Select(x => x.AppendixId is { } id
            ? new ContractTemplateRenderReference($"Căn cứ Phụ lục {appendices[id].AppendixCode} - {appendices[id].AppendixName} thuộc Hợp đồng số {source.Legal.ContractCode}.",
                $"Pursuant to Appendix {appendices[id].AppendixCode} - {appendices[id].AppendixNameEn} to Contract No. {source.Legal.ContractCode}.")
            : new ContractTemplateRenderReference($"Căn cứ Hợp đồng số {source.Legal.ContractCode} ký ngày {row.ContractSignDate:dd/MM/yyyy} giữa {source.Parties.Single(p => p.PartyRole == 1).LegalName} và {source.Parties.Single(p => p.PartyRole == 2).LegalName} về việc {source.Legal.ContractName}.",
                $"Pursuant to Contract No. {source.Legal.ContractCode} signed on {DocumentEnglishPolicy.Date(row.ContractSignDate)} between {source.Parties.Single(p => p.PartyRole == 1).LegalNameEn} and {source.Parties.Single(p => p.PartyRole == 2).LegalNameEn} regarding {source.Legal.ContractNameEn}.")).ToArray();
        var lines = await (from l in db.TblContractHandoverLines.AsNoTracking()
            join s in db.TblContractHandoverScopeItems.AsNoTracking() on l.HandoverScopeItemId equals s.HandoverScopeItemId
            where l.HandoverRecordId == recordId && s.HandoverBaselineId == row.HandoverBaselineId
            orderby l.DisplayOrder
            select new HandoverLineResponse(l.HandoverLineId, s.HandoverScopeItemId, l.Quantity, s.ItemCode, s.NameVi, s.NameEn, s.UnitVi, s.UnitEn, l.NotesVi, l.NotesEn)).ToListAsync(ct);
        var actorIds = new[] { row.CreatedEmployeeId, row.FinalizedByEmployeeId ?? 0, row.CancelledByEmployeeId ?? 0, row.SignedByEmployeeId ?? 0 };
        var actors = await db.TblEmployees.AsNoTracking().Where(x => actorIds.Contains(x.EmployeeId)).ToDictionaryAsync(x => x.EmployeeId, x => x.EmployeeFullName, ct);
        var artifacts = await db.TblFileStorages.AsNoTracking().Where(x => x.ObjectType == ArtifactType && x.ObjectId == recordId).Select(x => new ContractAcceptanceArtifactResponse {
            FileId = x.FileId, Format = x.FileType!, FileName = x.FileName, ContentType = x.ContentType!, FileSize = x.FileSize ?? 0, Sha256 = x.Sha256! }).ToListAsync(ct);
        var evidence = await (from e in db.TblContractHandoverEvidences.AsNoTracking()
            join f in db.TblFileStorages.AsNoTracking() on e.FileId equals f.FileId
            where e.HandoverRecordId == recordId && f.ObjectType == EvidenceType && f.ObjectId == recordId
            select new { e, f.FileName }).SingleOrDefaultAsync(ct);
        return new() {
            Record = Summary(row), TemplateVersionId = row.TemplateVersionId, TemplateName = templateName.TemplateName, ContractVersionNo = source.Version.VersionNo, TemplateVersionNo = templateName.VersionNo,
            ContractDirection = (ContractDirection)source.Legal.ContractDirection, LanguageMode = (ContractLanguageMode)source.Legal.LanguageMode,
            ContractSignDate = row.ContractSignDate, Location = row.Location, LocationEn = row.LocationEn, SnapshotHash = row.SnapshotHash,
            CreatedByEmployeeName = actors.GetValueOrDefault(row.CreatedEmployeeId), CreatedAt = row.CreatedAt,
            FinalizedByEmployeeName = actors.GetValueOrDefault(row.FinalizedByEmployeeId ?? 0), FinalizedAt = row.FinalizedAt,
            CancelledByEmployeeName = actors.GetValueOrDefault(row.CancelledByEmployeeId ?? 0), CancelledAt = row.CancelledAt, CancellationReason = row.CancellationReason,
            Parties = parties.Select(x => new HandoverPartyInput { SubjectKind = x.SubjectKind, LegalName = x.LegalName, LegalNameEn = x.LegalNameEn,
                Address = x.Address, AddressEn = x.AddressEn, Phone = x.Phone, Fax = x.Fax, TaxCode = x.TaxCode,
                RepresentativeName = x.RepresentativeName, RepresentativeTitle = x.RepresentativeTitle, RepresentativeTitleEn = x.RepresentativeTitleEn }).ToArray(),
            Sections = sectionRows.Select(x => new HandoverSectionInput { SectionCode = x.SectionCode, TitleVi = x.TitleVi, TitleEn = x.TitleEn,
                ContentVi = x.ContentVi, ContentEn = x.ContentEn, BilingualContentMode = x.BilingualContentMode }).ToArray(),
            AppendixIds = appendixIds, References = references, Lines = lines, Artifacts = artifacts,
            SignedEvidence = evidence is null ? null : new(evidence.e.HandoverEvidenceId, evidence.e.FileId, evidence.FileName, evidence.e.SignDate,
                evidence.e.UploadedByEmployeeId, actors.GetValueOrDefault(evidence.e.UploadedByEmployeeId), evidence.e.UploadedAt)
        };
    }
    private async Task<(byte[] Docx, IReadOnlyDictionary<string, string> Scalars)> RenderAsync(int contractId, int recordId, int employeeId, CancellationToken ct)
    {
        var detail = await GetAsync(contractId, recordId, employeeId, ct);
        var row = await RecordAsync(contractId, recordId, ct);
        var source = await SourceAsync(contractId, row.ContractVersionId, true, ct);
        var template = await TemplateAsync(source, row.TemplateVersionId, false, ct);
        if (detail.Parties.Count != 2 || detail.Lines.Count == 0) throw Rule("HandoverSnapshotIncomplete", "Biên bản thiếu hai bên hoặc phạm vi.");
        ValidateMetadata(new HandoverMetadataRequest { HandoverCode = row.HandoverCode, HandoverDate = row.HandoverDate, Location = row.Location, LocationEn = row.LocationEn }, source);
        await ValidateSectionsAsync(row.TemplateVersionId, detail.Sections, detail.LanguageMode, ct);
        var content = await ReadFileAsync(template.File, ct);
        if (!string.Equals(Convert.ToHexString(SHA256.HashData(content)), template.Version.DocumentHash, StringComparison.OrdinalIgnoreCase))
            throw Rule("HandoverTemplateHashMismatch", "DOCX nguồn không khớp hash của phiên bản mẫu.");
        var c = source.Contract; c.ContractCode = source.Legal.ContractCode; c.ContractName = source.Legal.ContractName;
        c.ContractNameEn = source.Legal.ContractNameEn; c.CreatedDate = source.Legal.ContractCreatedDate;
        c.EffectiveDate = source.Legal.EffectiveDate; c.ExpireDate = source.Legal.ExpireDate; c.SignDate = row.ContractSignDate;
        var external = FrozenContractPartySources.Counterparty(source.Parties.Single(x => x.PartyRole == 2));
        var internalParty = FrozenContractPartySources.Tenant(source.Parties.Single(x => x.PartyRole == 1));
        var ep = detail.Parties.Single(x => x.SubjectKind == 2); var ip = detail.Parties.Single(x => x.SubjectKind == 1);
        external.CustomerCompany = ep.LegalName; external.CustomerCompanyEn = ep.LegalNameEn;
        external.CustomerAddress = ep.Address; external.CustomerAddressEn = ep.AddressEn; external.CustomerPhone = ep.Phone; external.CustomerFaxNumber = ep.Fax; external.CustomerTaxCode = ep.TaxCode;
        external.CustomerContactPersonName = ep.RepresentativeName; external.CustomerContactPersonTitle = ep.RepresentativeTitle; external.CustomerContactPersonTitleEn = ep.RepresentativeTitleEn;
        internalParty.LegalEntityName = ip.LegalName; internalParty.LegalEntityNameEn = ip.LegalNameEn!;
        internalParty.Address = ip.Address!; internalParty.AddressEn = ip.AddressEn!; internalParty.PhoneNumber = ip.Phone; internalParty.FaxNumber = ip.Fax; internalParty.TaxCode = ip.TaxCode!;
        internalParty.RepresentativeName = ip.RepresentativeName; internalParty.RepresentativeTitle = ip.RepresentativeTitle!; internalParty.RepresentativeTitleEn = ip.RepresentativeTitleEn!;
        var fields = await db.TblContractTemplateFields.AsNoTracking().Where(x => x.TemplateVersionId == row.TemplateVersionId).ToListAsync(ct);
        TblEmployee? owner = null; TblDepartment? department = null;
        if (fields.Any(x => x.SourceFieldKey.StartsWith("contract-owner.") || x.SourceFieldKey.StartsWith("department.")))
        {
            owner = await db.TblEmployees.AsNoTracking().SingleOrDefaultAsync(x => x.EmployeeId == c.EmployeeId, ct);
            if (owner?.DepartmentId is { } id) department = await db.TblDepartments.AsNoTracking().SingleOrDefaultAsync(x => x.DepartmentId == id, ct);
        }
        var context = new ContractPlaceholderResolveContext(c, source.Version, external, internalParty, owner, department);
        var values = DocumentPartyMarkers.Values(detail.ContractDirection, DocumentParty.From(internalParty), DocumentParty.From(external));
        values["HANDOVER_CODE"] = row.HandoverCode; values["HANDOVER_DATE"] = row.HandoverDate.ToString("dd/MM/yyyy");
        values["HANDOVER_DATE_EN"] = DocumentEnglishPolicy.Date(row.HandoverDate); values["HANDOVER_LOCATION"] = row.Location; values["HANDOVER_LOCATION_EN"] = row.LocationEn ?? "";
        foreach (var field in fields.Where(x => !x.IsSystem))
        {
            var value = sources.Format(field.SourceFieldKey, sources.Resolve(field.SourceFieldKey, context), field.FormatString, field.DefaultValue);
            if (field.SourceFieldKey.EndsWith("-en", StringComparison.Ordinal) && !DocumentEnglishPolicy.IsOptionalPartyEnglish(
                ContractPlaceholderCatalog.FromSnapshot(field), detail.ContractDirection)) value = DocumentEnglishPolicy.Require(value, field.FieldLabel);
            values[field.PlaceholderKey] = value;
        }
        var data = new ContractTemplateRenderData(values, [], detail.Sections.Select((x, i) =>
            new ContractTemplateRenderTerm(i + 1, x.TitleVi, x.TitleEn ?? "", x.ContentVi ?? "", x.ContentEn ?? "") { BilingualContentMode = x.BilingualContentMode }).ToArray(),
            new("", ""), new("", ""), "") {
            ContractDirection = detail.ContractDirection, Definitions = fields.Select(ContractPlaceholderCatalog.FromSnapshot).ToArray(),
            DocumentReferences = detail.References,
            HandoverItems = detail.Lines.Select((x, i) => new ContractTemplateRenderHandoverItem(i + 1, x.Code, x.NameVi, x.NameEn, x.UnitVi, x.UnitEn, x.Quantity, x.NotesVi, x.NotesEn)).ToArray()
        };
        return (renderer.Render(content, detail.LanguageMode, data), values);
    }
}
