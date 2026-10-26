using System.Security.Cryptography;
using ContractManagement.API.Common.Enums;
using ContractManagement.API.Domains.Models.Contract;
using ContractManagement.Common.Enums;
using ContractManagement.Domains.Interfaces.Contract;
using ContractManagement.Domains.Interfaces.ContractTemplate;
using ContractManagement.Domains.Interfaces.File;
using ContractManagement.Domains.Policies.ContractTemplate;
using ContractManagement.Domains.Services.ContractTemplate;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Domains.Services.Contract;

/// <summary>
/// Renders SoftwareSupply DOCX/PDF from one typed snapshot. Preview results
/// remain ephemeral; the submit pipeline persists the separate submission result
/// only after both formats have been generated successfully.
/// </summary>
public sealed class ContractDocumentPreviewService :
    IContractDocumentPreviewService,
    IContractSubmissionArtifactRenderer
{
    private const string TemplateVersionObjectType = "ContractTemplateVersion";
    private const string DocxContentType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string PdfContentType = "application/pdf";

    private readonly DbDtctechContext _dbContext;
    private readonly IContractResourceAuthorizationService _authorization;
    private readonly IFileStorageService _fileStorageService;
    private readonly IContractTemplatePreviewRenderer _documentRenderer;
    private readonly IContractTemplatePdfRenderer _pdfRenderer;
    private readonly IContractPlaceholderValueService _placeholderValues;

    public ContractDocumentPreviewService(
        DbDtctechContext dbContext,
        IContractResourceAuthorizationService authorization,
        IFileStorageService fileStorageService,
        IContractTemplatePreviewRenderer documentRenderer,
        IContractTemplatePdfRenderer pdfRenderer,
        IContractPlaceholderValueService? placeholderValues = null)
    {
        _dbContext = dbContext;
        _authorization = authorization;
        _fileStorageService = fileStorageService;
        _documentRenderer = documentRenderer;
        _pdfRenderer = pdfRenderer;
        _placeholderValues = placeholderValues ?? new ContractPlaceholderValueService(dbContext, new ContractPlaceholderSourceRegistry());
    }

    public async Task<ContractDocumentPreviewResult> GenerateDocxAsync(
        int contractId,
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        var rendered = await RenderDocxAsync(
            contractId,
            employeeId,
            requireNegotiating: false,
            cancellationToken);

        return new ContractDocumentPreviewResult(
            rendered.Content,
            $"{rendered.SafeContractCode}-preview.docx",
            DocxContentType);
    }

    public async Task<ContractDocumentPreviewResult> GeneratePdfAsync(
        int contractId,
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        var rendered = await RenderDocxAsync(
            contractId,
            employeeId,
            requireNegotiating: false,
            cancellationToken);
        var pdf = await _pdfRenderer.ConvertPreviewToPdfAsync(
            rendered.Content,
            cancellationToken);

        return new ContractDocumentPreviewResult(
            pdf,
            $"{rendered.SafeContractCode}-preview.pdf",
            PdfContentType);
    }

    public async Task<ContractSubmissionArtifactRenderResult> RenderAsync(
        int contractId,
        int employeeId,
        CancellationToken cancellationToken = default)
    {
        var rendered = await RenderDocxAsync(
            contractId,
            employeeId,
            requireNegotiating: true,
            cancellationToken);
        var pdf = await _pdfRenderer.ConvertPreviewToPdfAsync(
            rendered.Content,
            cancellationToken);

        return new ContractSubmissionArtifactRenderResult(
            rendered.Snapshot,
            rendered.TemplateVersionId,
            rendered.Content,
            $"{rendered.SafeContractCode}-submitted.docx",
            pdf,
            $"{rendered.SafeContractCode}-submitted.pdf");
    }

    private async Task<RenderedContractDocx> RenderDocxAsync(
        int contractId,
        int employeeId,
        bool requireNegotiating,
        CancellationToken cancellationToken)
    {
        // Preview is a write-scope operation: Manager may read another owner's
        // contract but may not render/export its mutable legal content.
        await _authorization.EnsureCanWriteAsync(
            contractId,
            employeeId,
            cancellationToken);

        var contract = await _dbContext.TblContracts
            .AsNoTracking()
            .SingleAsync(item => item.ContractId == contractId, cancellationToken);

        EnsurePreviewPolicy(contract);
        if (requireNegotiating
            && contract.Status != (byte)ContractStatus.Negotiating)
        {
            throw new InvalidOperationException(
                "Chỉ hợp đồng đang đàm phán mới được tạo artifact gửi duyệt.");
        }

        var versionId = contract.CurrentVersionId
            ?? throw new InvalidOperationException(
                "Hợp đồng chưa có phiên bản hiện hành để tạo preview.");
        var version = await _dbContext.TblContractVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.VersionId == versionId
                    && item.ContractId == contractId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "Phiên bản hiện hành của hợp đồng không còn khả dụng.");

        var templateVersionId = version.TemplateVersionId;

        var template = await (
                from templateVersion in _dbContext.TblContractTemplateVersions
                    .AsNoTracking()
                join templateDefinition in _dbContext.TblContractTemplates
                    .AsNoTracking()
                    on templateVersion.TemplateId equals templateDefinition.TemplateId
                where templateVersion.TemplateVersionId == templateVersionId
                select new { Version = templateVersion, Template = templateDefinition })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "Phiên bản template của hợp đồng không còn khả dụng.");

        EnsureTemplatePolicy(template.Version, template.Template, contract);

        var customer = await _dbContext.TblCustomers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.CustomerId == contract.CustomerId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "Khách hàng của hợp đồng không còn khả dụng.");
        var tenant = await _dbContext.TblTenantLegalProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "Hồ sơ pháp lý doanh nghiệp chưa được cấu hình.");
        var items = await _dbContext.TblContractItems
            .AsNoTracking()
            .Where(item => item.ContractId == contractId
                && item.VersionId == versionId)
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.ContractItemId)
            .ToListAsync(cancellationToken);
        var terms = await _dbContext.TblContractTerms
            .AsNoTracking()
            .Where(term => term.ContractId == contractId
                && term.VersionId == versionId)
            .OrderBy(term => term.DisplayOrder)
            .ThenBy(term => term.TermId)
            .ToListAsync(cancellationToken);
        var legalBases = await _dbContext.TblContractLegalBases
            .AsNoTracking()
            .Where(item => item.ContractId == contractId
                && item.VersionId == versionId)
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.LegalBasisId)
            .ToListAsync(cancellationToken);
        var paymentMilestones = await _dbContext.TblContractPaymentMilestones
            .AsNoTracking()
            .Where(item => item.ContractId == contractId && item.VersionId == versionId)
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.PaymentMilestoneId)
            .ToListAsync(cancellationToken);
        var appendices = await _dbContext.TblContractAppendices
            .AsNoTracking()
            .Where(item => item.ContractId == contractId
                && item.VersionId == versionId)
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.AppendixId)
            .ToListAsync(cancellationToken);
        var appendixIds = appendices.Select(item => item.AppendixId).ToList();
        var appendixTerms = await _dbContext.TblContractAppendixTerms
            .AsNoTracking()
            .Where(item => appendixIds.Contains(item.AppendixId))
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.AppendixTermId)
            .ToListAsync(cancellationToken);

        var snapshot = SoftwareSupplyContractSnapshotFactory.Create(
            tenant,
            customer,
            contract,
            version,
            items,
            terms,
            paymentMilestones,
            appendices,
            appendixTerms);
        snapshot = snapshot with
        {
            LegalBases = legalBases.Select(item => new ContractLegalBasisSnapshot(
                item.LegalBasisId, item.BasisCode, item.ContentVi,
                item.ContentEn, item.DisplayOrder)).ToArray()
        };
        var itemLayoutRows = await _dbContext
            .TblContractTemplateItemTableColumnLayouts
            .AsNoTracking()
            .Where(item => item.TemplateVersionId == templateVersionId)
            .OrderBy(item => item.DisplayOrder)
            .Select(item => new
            {
                item.ColumnKey,
                item.DisplayOrder,
                item.WidthBps
            })
            .ToListAsync(cancellationToken);
        var itemTableWidths = ContractTableLayoutPolicy.RequireItemColumnWidths(
            itemLayoutRows.Select(item => (
                item.ColumnKey,
                (int)item.DisplayOrder,
                (int)item.WidthBps)));
        var fields = await _dbContext.TblContractTemplateFields.AsNoTracking()
            .Where(x => x.TemplateVersionId == templateVersionId).ToListAsync(cancellationToken);
        var renderData = CreateRenderData(snapshot, fields.Any(x => x.PlaceholderKey == "CONTRACT_APPENDICES")) with
        {
            ItemTableColumnWidthsBps = itemTableWidths
        };
        var definitions = fields.Select(ContractPlaceholderCatalog.FromSnapshot).ToArray();
        var custom = await _placeholderValues
            .CaptureAsync(contract, version, cancellationToken: cancellationToken,
                preloadedContext: new ContractPlaceholderResolveContext(contract, version, customer, tenant, null, null));
        var scalars = new Dictionary<string, string>(renderData.ScalarValues, StringComparer.Ordinal);
        // Resolve custom bindings from the version; locked values are never read from master again.
        foreach (var pair in custom) scalars[pair.Key] = pair.Value;
        renderData = renderData with
        {
            ScalarValues = scalars,
            Definitions = definitions
        };
        // Included in the canonical relational aggregate/hash alongside the artifact.
        snapshot = snapshot with { PlaceholderValues = scalars };
        var source = await ReadTemplateSourceAsync(
            template.Version,
            cancellationToken);
        var renderedDocx = _documentRenderer.Render(
            source,
            (ContractLanguageMode)snapshot.Contract.LanguageMode,
            renderData);

        return new RenderedContractDocx(
            renderedDocx,
            SafeFileName(snapshot.Contract.ContractCode, contractId),
            snapshot,
            templateVersionId);
    }

    private static void EnsurePreviewPolicy(TblContract contract)
    {
        if ((ContractStatus)contract.Status is not (
            ContractStatus.Draft or ContractStatus.Negotiating))
        {
            throw new InvalidOperationException(
                "Chỉ hợp đồng nháp hoặc đang đàm phán mới được tạo bản preview động.");
        }

        if (!Enum.IsDefined((ContractType)contract.ContractType))
        {
            throw new InvalidOperationException(
                "Loại hợp đồng không hỗ trợ kết xuất tài liệu.");
        }

    }

    private static void EnsureTemplatePolicy(
        TblContractTemplateVersion version,
        TblContractTemplate template,
        TblContract contract)
    {
        var expectedType = (ContractType)contract.ContractType switch {
            ContractType.SoftwareSupply => TemplateDocumentType.SoftwareSupplyContract,
            ContractType.SoftwareMaintenance => TemplateDocumentType.SoftwareMaintenanceContract,
            ContractType.SoftwareUpkeep => TemplateDocumentType.SoftwareUpkeepContract,
            _ => throw new InvalidOperationException("Loại hợp đồng không hợp lệ.")
        };
        if (template.DocumentType != (byte)expectedType)
        {
            throw new InvalidOperationException(
                "Loại mẫu không khớp với loại hợp đồng.");
        }

        if (version.ValidationStatus != (byte)TemplateValidationStatus.Valid)
        {
            throw new InvalidOperationException(
                "Template của hợp đồng chưa vượt qua kiểm tra placeholder.");
        }

        if (version.Status is not ((byte)TemplateVersionStatus.Published)
            and not ((byte)TemplateVersionStatus.Retired))
        {
            throw new InvalidOperationException(
                "Template của hợp đồng chưa từng được publish.");
        }

        if (version.ContractDirection != contract.ContractDirection)
            throw new InvalidOperationException("Chiều giao dịch của mẫu không khớp với hợp đồng.");
        if (template.LanguageMode != contract.LanguageMode)
        {
            throw new InvalidOperationException(
                "Ngôn ngữ template không khớp với hợp đồng.");
        }
    }

    private async Task<byte[]> ReadTemplateSourceAsync(
        TblContractTemplateVersion templateVersion,
        CancellationToken cancellationToken)
    {
        var fileId = templateVersion.DocumentFileId
            ?? throw new InvalidOperationException(
                "Template chưa có DOCX nguồn để tạo preview.");
        var metadata = await _dbContext.TblFileStorages
            .AsNoTracking()
            .SingleOrDefaultAsync(file => file.FileId == fileId, cancellationToken);
        if (metadata is null
            || metadata.ObjectType != TemplateVersionObjectType
            || metadata.ObjectId != templateVersion.TemplateVersionId)
        {
            throw new InvalidOperationException(
                "Metadata DOCX nguồn không thuộc phiên bản template của hợp đồng.");
        }

        var storedDocument = await _fileStorageService.DownloadAsync(fileId);
        if (storedDocument is null)
        {
            throw new InvalidOperationException(
                "DOCX nguồn của template không còn khả dụng.");
        }

        await using var stream = storedDocument.Value.Stream;
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        if (!string.IsNullOrWhiteSpace(templateVersion.DocumentHash))
        {
            var actualHash = Convert.ToHexString(SHA256.HashData(bytes));
            if (!string.Equals(
                    actualHash,
                    templateVersion.DocumentHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "DOCX nguồn không khớp hash đã validation của template.");
            }
        }

        return bytes;
    }

    private static ContractTemplateRenderData CreateRenderData(
        SoftwareSupplyContractSnapshot snapshot,
        bool renderAppendices)
    {
        var contract = snapshot.Contract;
        var version = snapshot.Version;
        var customerSnapshot = snapshot.Customer;
        var tenant = snapshot.Tenant;
        var currency = version.CurrencyCode;
        _ = Required(
            customerSnapshot.ContactName,
            "Người liên lạc khách hàng");

        var scalarValues = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CONTRACT_DATE_EN"] = DocumentEnglishPolicy.Date(contract.CreatedDate),
            ["EFFECTIVE_DATE_EN"] = contract.EffectiveDate is { } effective ? DocumentEnglishPolicy.Date(effective) : string.Empty,
            ["EXPIRE_DATE_EN"] = contract.ExpireDate is { } expiry ? DocumentEnglishPolicy.Date(expiry) : string.Empty,
            ["CONTRACT_TOTAL_AMOUNT_IN_WORDS_EN"] = EnglishMoneyTextFormatter.Format(version.TotalAmount, currency),
            ["CONTRACT_CODE"] = Required(contract.ContractCode, "Mã hợp đồng"),
            ["CONTRACT_NAME"] = Required(contract.ContractName, "Tên hợp đồng"),
            ["CONTRACT_NAME_EN"] = Value(contract.ContractNameEn),
            ["CONTRACT_DATE"] = FormatDate(contract.CreatedDate),
            ["EFFECTIVE_DATE"] = FormatRequiredDate(
                contract.EffectiveDate,
                "Ngày hiệu lực"),
            ["EXPIRE_DATE"] = FormatRequiredDate(
                contract.ExpireDate,
                "Ngày hết hạn"),
            ["CONTRACT_CURRENCY"] = currency,
            ["CONTRACT_TOTAL_AMOUNT"] = FormatAmount(version.TotalAmount, currency),
            ["CONTRACT_TOTAL_AMOUNT_IN_WORDS"] =
                VietnameseMoneyTextFormatter.Format(version.TotalAmount, currency),
        };

        foreach (var pair in DocumentPartyMarkers.Values((ContractDirection)contract.ContractDirection,
                     DocumentParty.From(tenant), DocumentParty.From(customerSnapshot)))
            scalarValues[pair.Key] = pair.Value;

        var items = snapshot.Items.Select((item, index) =>
            new ContractTemplateRenderItem(
                index + 1,
                item.ItemType == (byte)ContractItemType.Product
                    ? "Sản phẩm"
                    : "Dịch vụ",
                BuildItemDescription(item),
                item.Quantity,
                item.UnitPrice,
                item.DiscountMode == (byte)ContractItemDiscountMode.Percentage
                    ? $"{item.DiscountPercent:0.####}%"
                    : FormatMoney(item.DiscountAmount, currency),
                item.IsTaxable ? $"{item.VatPercent:0.####}%" : "0%",
                item.LineTotal)
            {
                TypeEn = item.ItemType == (byte)ContractItemType.Product ? "Product" : "Service",
                DescriptionEn = string.IsNullOrWhiteSpace(item.ItemNameEn) ? null
                    : string.IsNullOrWhiteSpace(item.ItemDescriptionEn) ? item.ItemNameEn
                    : $"{item.ItemNameEn} — {item.ItemDescriptionEn}"
            })
            .ToArray();

        var milestones = snapshot.PaymentMilestones ?? [];
        var terms = snapshot.Terms.Select((term, index) =>
            new ContractTemplateRenderTerm(
                index + 1,
                term.TermTitle,
                Value(term.TermTitleEn),
                Value(term.TermContent),
                Value(term.TermContentEn))
            {
                Kind = (ContractTermKind)term.TermKind,
                BilingualContentMode = term.BilingualContentMode,
                PaymentMilestones = milestones.Where(item => item.TermId == term.TermId)
                    .OrderBy(item => item.DisplayOrder)
                    .Select((item, milestoneIndex) => new ContractTemplateRenderPaymentMilestone(
                        milestoneIndex + 1, item.TitleVi, item.TitleEn,
                        item.PaymentPercent, item.Amount,
                        (PaymentDueAnchor)item.DueAnchor, item.DueOffsetDays,
                        (PaymentDayCountMode)item.DayCountMode,
                        item.ConditionVi, item.ConditionEn, item.DueDate)).ToArray()
            })
            .ToArray();

        return new ContractTemplateRenderData(
            scalarValues,
            items,
            terms,
            new ContractTemplateRenderSignature("ĐẠI DIỆN BÊN CUNG CẤP", string.Empty),
            new ContractTemplateRenderSignature("ĐẠI DIỆN BÊN KHÁCH HÀNG", string.Empty),
            string.Empty,
            currency)
        {
            ContractDirection = (ContractDirection)contract.ContractDirection,
            LegalBases = (snapshot.LegalBases ?? [])
                .OrderBy(item => item.DisplayOrder)
                .ThenBy(item => item.LegalBasisId)
                .Select((item, index) => new ContractTemplateRenderLegalBasis(
                    index + 1, item.ContentVi, item.ContentEn))
                .ToArray(),
            Appendices = (renderAppendices ? snapshot.Appendices ?? [] : [])
                .OrderBy(item => item.DisplayOrder)
                .ThenBy(item => item.AppendixId)
                .Select((appendix, appendixIndex) =>
                    new ContractTemplateRenderAppendix(
                        appendixIndex + 1,
                        appendix.AppendixCode,
                        appendix.AppendixName,
                        appendix.AppendixNameEn,
                        BuildAppendixReferenceVi(snapshot),
                        (ContractLanguageMode)contract.LanguageMode ==
                            ContractLanguageMode.Bilingual
                            ? BuildAppendixReferenceEn(snapshot)
                            : null,
                        appendix.Terms
                            .OrderBy(term => term.DisplayOrder)
                            .ThenBy(term => term.AppendixTermId)
                            .Select((term, termIndex) =>
                                new ContractTemplateRenderAppendixTerm(
                                    termIndex + 1,
                                    term.TermTitle,
                                    term.TermTitleEn,
                                    term.TermContent,
                                    term.TermContentEn) { BilingualContentMode = term.BilingualContentMode })
                            .ToArray()))
                .ToArray()
        };
    }

    private static string BuildAppendixReferenceVi(
        SoftwareSupplyContractSnapshot snapshot) =>
        $"Căn cứ Hợp đồng số {snapshot.Contract.ContractCode} giữa "
        + $"{snapshot.Tenant.LegalEntityName} và {snapshot.Customer.LegalName} "
        + $"về việc {snapshot.Contract.ContractName}.";

    private static string BuildAppendixReferenceEn(
        SoftwareSupplyContractSnapshot snapshot) =>
        $"Pursuant to Contract No. {snapshot.Contract.ContractCode} between "
        + $"{DocumentEnglishPolicy.Require(snapshot.Tenant.LegalNameEn, "tên pháp nhân tiếng Anh doanh nghiệp")} and {DocumentEnglishPolicy.OptionalCustomerEnglish(snapshot.Customer.LegalNameEn)} "
        + $"regarding {DocumentEnglishPolicy.Require(snapshot.Contract.ContractNameEn, "tên hợp đồng tiếng Anh")}.";

    private static string BuildItemDescription(ContractItemLegalSnapshot item) =>
        string.IsNullOrWhiteSpace(item.ItemDescription)
            ? item.ItemName
            : $"{item.ItemName} — {item.ItemDescription}";

    private static string FormatDate(DateTime value) =>
        $"Ngày {value:dd} tháng {value:MM} năm {value:yyyy}";

    private static string FormatRequiredDate(DateTime? value, string fieldName) =>
        value.HasValue
            ? FormatDate(value.Value)
            : throw new InvalidOperationException($"{fieldName} chưa được cấu hình.");

    private static string FormatMoney(decimal value, string currency) =>
        $"{FormatAmount(value, currency)} {currency}";

    private static string FormatAmount(decimal value, string currency)
    {
        var decimals = currency.Equals("USD", StringComparison.OrdinalIgnoreCase) ? 2 : 0;
        var culture = System.Globalization.CultureInfo.GetCultureInfo(decimals == 0 ? "vi-VN" : "en-US");
        return decimal.Round(value, decimals, MidpointRounding.AwayFromZero)
            .ToString(decimals == 0 ? "N0" : "N2", culture);
    }

    private static string Required(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{fieldName} chưa được cấu hình.");
        }

        return value.Trim();
    }

    private static string Value(string? value) => value?.Trim() ?? string.Empty;

    private static string SafeFileName(string contractCode, int contractId)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var normalized = new string(contractCode
            .Select(character => invalid.Contains(character) ? '-' : character)
            .ToArray()).Trim().Trim('.');
        return string.IsNullOrWhiteSpace(normalized)
            ? $"contract-{contractId}"
            : normalized;
    }

    private sealed record RenderedContractDocx(
        byte[] Content,
        string SafeContractCode,
        SoftwareSupplyContractSnapshot Snapshot,
        int TemplateVersionId);
}

internal static class VietnameseMoneyTextFormatter
{
    private static readonly string[] Digits =
        ["không", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín"];

    public static string Format(decimal amount, string currencyCode)
    {
        var isUsd = string.Equals(currencyCode, "USD", StringComparison.OrdinalIgnoreCase);
        var rounded = decimal.Round(amount, isUsd ? 2 : 0, MidpointRounding.AwayFromZero);
        if (rounded < 0 || rounded > long.MaxValue)
        {
            return $"{rounded:N0} {currencyCode}".Replace(',', '.');
        }

        var whole = decimal.Truncate(rounded);
        var value = decimal.ToInt64(whole);
        var number = value == 0 ? "Không" : Capitalize(ReadNumber(value));
        var unit = string.Equals(currencyCode, "VND", StringComparison.OrdinalIgnoreCase)
            ? "đồng chẵn"
            : currencyCode;
        var cents = (int)((rounded - whole) * 100);
        return isUsd && cents > 0 ? $"{number} {unit} và {ReadNumber(cents)} xu" : $"{number} {unit}";
    }

    private static string ReadNumber(long value)
    {
        var scales = new[] { "", "nghìn", "triệu", "tỷ", "nghìn tỷ", "triệu tỷ" };
        var groups = new List<int>();
        while (value > 0)
        {
            groups.Add((int)(value % 1000));
            value /= 1000;
        }

        var parts = new List<string>();
        for (var index = groups.Count - 1; index >= 0; index--)
        {
            if (groups[index] == 0)
            {
                continue;
            }

            var forceHundreds = index < groups.Count - 1 && groups[index] < 100;
            parts.Add(ReadGroup(groups[index], forceHundreds));
            if (!string.IsNullOrEmpty(scales[index]))
            {
                parts.Add(scales[index]);
            }
        }

        return string.Join(' ', parts);
    }

    private static string ReadGroup(int value, bool forceHundreds)
    {
        var hundreds = value / 100;
        var tens = value % 100 / 10;
        var ones = value % 10;
        var parts = new List<string>();

        if (hundreds > 0 || forceHundreds)
        {
            parts.Add(Digits[hundreds]);
            parts.Add("trăm");
        }

        if (tens > 1)
        {
            parts.Add(Digits[tens]);
            parts.Add("mươi");
        }
        else if (tens == 1)
        {
            parts.Add("mười");
        }
        else if (ones > 0 && (hundreds > 0 || forceHundreds))
        {
            parts.Add("lẻ");
        }

        if (ones > 0)
        {
            parts.Add(ones switch
            {
                1 when tens > 1 => "mốt",
                4 when tens > 1 => "tư",
                5 when tens > 0 => "lăm",
                _ => Digits[ones]
            });
        }

        return string.Join(' ', parts);
    }

    private static string Capitalize(string value) =>
        string.IsNullOrEmpty(value)
            ? value
            : char.ToUpperInvariant(value[0]) + value[1..];
}
