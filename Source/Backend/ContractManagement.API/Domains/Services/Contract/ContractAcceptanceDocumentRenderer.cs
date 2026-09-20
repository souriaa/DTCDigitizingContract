using ContractManagement.API.Common.Enums;
using ContractManagement.API.Domains.DTOs.Responses.Contract;
using ContractManagement.Domains.Policies.ContractTemplate;
using ContractManagement.Domains.Services.ContractTemplate;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ContractManagement.Domains.Services.Contract;

public sealed record ContractAcceptanceDocumentRenderData(
    string AcceptanceCode,
    DateTime AcceptanceDate,
    string Location,
    ContractLanguageMode LanguageMode,
    IReadOnlyList<ContractAcceptanceReferenceResponse> References,
    IReadOnlyList<ContractAcceptancePartyResponse> Parties,
    IReadOnlyList<ContractAcceptanceSectionResponse> Sections)
{
    public IReadOnlyDictionary<string, string> CustomValues { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

public sealed class ContractAcceptanceDocumentRenderer
{
    public const string ReferencesMarker = "{{DOCUMENT_REFERENCES}}";
    public const string SectionsMarker = "{{ACCEPTANCE_SECTIONS}}";

    public byte[] Render(byte[] source, ContractAcceptanceDocumentRenderData data)
    {
        if (source.Length == 0)
            throw new InvalidOperationException("Acceptance template không có DOCX nguồn.");

        using var output = new MemoryStream();
        output.Write(source);
        output.Position = 0;
        using (var document = WordprocessingDocument.Open(output, true))
        {
            var body = document.MainDocumentPart?.Document?.Body
                ?? throw new InvalidOperationException(
                    "Acceptance template không có document body.");
            ReplaceOptionalMarker(body, ReferencesMarker,
                (_, _) => CreateReferenceParagraphs(data));
            ReplaceOptionalMarker(body, SectionsMarker,
                (_, width) => CreateSectionElements(data, width));

            var customer = data.Parties.Single(x =>
                x.PartyRole == AcceptancePartyRole.Customer);
            var tenant = data.Parties.Single(x =>
                x.PartyRole == AcceptancePartyRole.Tenant);
            var values = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ACCEPTANCE_CODE"] = data.AcceptanceCode,
                ["ACCEPTANCE_DATE"] = data.AcceptanceDate.ToString("dd/MM/yyyy"),
                ["ACCEPTANCE_LOCATION"] = data.Location,
                ["CUSTOMER_LEGAL_NAME"] = customer.LegalName,
                ["CUSTOMER_ADDRESS"] = customer.Address ?? string.Empty,
                ["CUSTOMER_PHONE"] = customer.Phone ?? string.Empty,
                ["CUSTOMER_FAX"] = customer.Fax ?? string.Empty,
                ["CUSTOMER_TAX_CODE"] = customer.TaxCode ?? string.Empty,
                ["CUSTOMER_REPRESENTATIVE_NAME"] = customer.RepresentativeName,
                ["CUSTOMER_REPRESENTATIVE_TITLE"] =
                    customer.RepresentativeTitle ?? string.Empty,
                ["TENANT_LEGAL_NAME"] = tenant.LegalName,
                ["TENANT_ADDRESS"] = tenant.Address ?? string.Empty,
                ["TENANT_PHONE"] = tenant.Phone ?? string.Empty,
                ["TENANT_FAX"] = tenant.Fax ?? string.Empty,
                ["TENANT_TAX_CODE"] = tenant.TaxCode ?? string.Empty,
                ["TENANT_REPRESENTATIVE_NAME"] = tenant.RepresentativeName,
                ["TENANT_REPRESENTATIVE_TITLE"] =
                    tenant.RepresentativeTitle ?? string.Empty
            };
            foreach (var (key, value) in data.CustomValues)
                values[key] = value;

            ReplaceOptionalMarker(body, "{{SIGNATURE_CUSTOMER}}",
                (_, _) => CreateSignatureParagraphs(customer, "ĐẠI DIỆN BÊN A"));
            ReplaceOptionalMarker(body, "{{SIGNATURE_PROVIDER}}",
                (_, _) => CreateSignatureParagraphs(tenant, "ĐẠI DIỆN BÊN B"));
            foreach (var paragraph in body.Descendants<W.Paragraph>())
                ReplaceScalarTokens(paragraph, values);
            document.MainDocumentPart!.Document.Save();
        }
        return output.ToArray();
    }

    private static void ReplaceOptionalMarker(
        W.Body body,
        string marker,
        Func<W.Paragraph, int, IReadOnlyList<OpenXmlElement>> createReplacements)
    {
        var matches = body.Descendants<W.Paragraph>()
            .Where(paragraph => string.Equals(
                paragraph.InnerText.Trim(), marker, StringComparison.Ordinal))
            .ToList();
        if (matches.Count > 1)
            throw new InvalidOperationException(
                $"Acceptance template chỉ được có tối đa một marker {marker}.");
        if (matches.Count == 0) return;
        var target = matches[0];
        var replacements = createReplacements(target,
            ContractTemplatePreviewRenderer.GetAvailableWidthDxa(target));
        foreach (var replacement in replacements)
        {
            ApplyPlaceholderFormatting(target, replacement);
            target.InsertBeforeSelf(replacement);
        }
        target.Remove();
    }

    private static void ApplyPlaceholderFormatting(
        W.Paragraph source,
        OpenXmlElement replacement)
    {
        if (replacement is not W.Paragraph paragraph) return;

        var sourceParagraphProperties = source.ParagraphProperties;
        var sourceJustification = sourceParagraphProperties?
            .GetFirstChild<W.Justification>();
        var sourceStyle = sourceParagraphProperties?
            .GetFirstChild<W.ParagraphStyleId>();
        if (sourceJustification is not null || sourceStyle is not null)
        {
            paragraph.ParagraphProperties ??= new W.ParagraphProperties();
            if (sourceJustification is not null)
            {
                paragraph.ParagraphProperties
                    .RemoveAllChildren<W.Justification>();
                paragraph.ParagraphProperties.Append(
                    sourceJustification.CloneNode(true));
            }
            if (sourceStyle is not null && paragraph.ParagraphProperties
                    .GetFirstChild<W.ParagraphStyleId>() is null)
            {
                paragraph.ParagraphProperties.PrependChild(
                    (W.ParagraphStyleId)sourceStyle.CloneNode(true));
            }
        }

        var sourceRunProperties = source.Descendants<W.Run>()
            .Select(run => run.RunProperties)
            .FirstOrDefault(properties => properties is not null);
        if (sourceRunProperties is null) return;
        foreach (var run in paragraph.Descendants<W.Run>())
        {
            run.RunProperties ??= new W.RunProperties();
            CopyRunProperty<W.RunFonts>(sourceRunProperties, run.RunProperties);
            CopyRunProperty<W.FontSize>(sourceRunProperties, run.RunProperties);
            CopyRunProperty<W.FontSizeComplexScript>(
                sourceRunProperties, run.RunProperties);
        }
    }

    private static void CopyRunProperty<T>(
        W.RunProperties source,
        W.RunProperties target)
        where T : OpenXmlElement
    {
        var value = source.GetFirstChild<T>();
        if (value is null) return;
        target.RemoveAllChildren<T>();
        target.Append(value.CloneNode(true));
    }

    private static IReadOnlyList<OpenXmlElement> CreateReferenceParagraphs(
        ContractAcceptanceDocumentRenderData data)
    {
        var result = new List<W.Paragraph>();
        foreach (var reference in data.References.OrderBy(x => x.DisplayOrder))
        {
            result.Add(Paragraph(reference.TextVi, italic: true));
            if (data.LanguageMode == ContractLanguageMode.Bilingual
                && !string.IsNullOrWhiteSpace(reference.TextEn))
                result.Add(Paragraph(reference.TextEn!, italic: true));
        }
        return result.Cast<OpenXmlElement>().ToList();
    }

    private static IReadOnlyList<OpenXmlElement> CreateSignatureParagraphs(
        ContractAcceptancePartyResponse party,
        string partyTitle) =>
    [
        Paragraph(partyTitle, bold: true),
        Paragraph("(Chữ ký)", italic: true),
        Paragraph(string.Empty),
        Paragraph(string.Empty),
        Paragraph(party.RepresentativeName, bold: true)
    ];

    private static IReadOnlyList<OpenXmlElement> CreateSectionElements(
        ContractAcceptanceDocumentRenderData data,
        int availableWidthDxa)
    {
        var result = new List<OpenXmlElement>();
        foreach (var section in data.Sections.OrderBy(x => x.DisplayOrder))
        {
            result.Add(Paragraph(section.TitleVi, bold: true));
            result.AddRange(ContentElements(section.ContentVi, availableWidthDxa));
            if (data.LanguageMode == ContractLanguageMode.Bilingual)
            {
                result.Add(Paragraph(section.TitleEn ?? string.Empty, bold: true));
                result.AddRange(ContentElements(section.ContentEn, availableWidthDxa));
            }
        }
        return result;
    }

    private static IEnumerable<OpenXmlElement> ContentElements(
        string? value, int availableWidthDxa) =>
        ContractTemplatePreviewRenderer.CreateRichTextElements(
            value,
            "AcceptanceRichTextInvalid",
            "Nội dung section không đúng rich text schema hiện hành.",
            availableWidthDxa);

    private static W.Paragraph Paragraph(
        string text, bool bold = false, bool italic = false)
    {
        var properties = new W.RunProperties();
        if (bold) properties.Append(new W.Bold());
        if (italic) properties.Append(new W.Italic());
        properties.Append(new W.RunFonts
        {
            Ascii = "Times New Roman",
            HighAnsi = "Times New Roman",
            EastAsia = "Times New Roman"
        });
        properties.Append(new W.FontSize { Val = "24" });
        return new W.Paragraph(new W.Run(
            properties,
            new W.Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    }

    private static void ReplaceScalarTokens(
        W.Paragraph paragraph,
        IReadOnlyDictionary<string, string> values)
    {
        var textElements = paragraph.Descendants<W.Text>().ToList();
        if (textElements.Count == 0) return;
        var text = string.Concat(textElements.Select(element => element.Text));
        var replaced = text;
        foreach (var (key, value) in values)
            replaced = replaced.Replace(
                $"{{{{{key}}}}}", value, StringComparison.Ordinal);
        if (replaced == text) return;
        textElements[0].Text = replaced;
        textElements[0].Space = SpaceProcessingModeValues.Preserve;
        foreach (var textElement in textElements.Skip(1))
            textElement.Text = string.Empty;
    }
}
