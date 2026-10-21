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
    public ContractDirection ContractDirection { get; init; }
    public string? LocationEn { get; init; }
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
                ["ACCEPTANCE_DATE_EN"] = DocumentEnglishPolicy.Date(data.AcceptanceDate),
                ["ACCEPTANCE_LOCATION_EN"] = data.LocationEn ?? string.Empty,
                ["ACCEPTANCE_CODE"] = data.AcceptanceCode,
                ["ACCEPTANCE_DATE"] = data.AcceptanceDate.ToString("dd/MM/yyyy"),
                ["ACCEPTANCE_LOCATION"] = data.Location,
            };
            foreach (var pair in DocumentPartyMarkers.Values(data.ContractDirection, DocumentParty.From(tenant), DocumentParty.From(customer)))
                values[pair.Key] = pair.Value;
            foreach (var (key, value) in data.CustomValues)
                values[key] = value;

            ReplaceOptionalMarker(body, "{{SIGNATURE_CUSTOMER}}",
                (_, _) => [Paragraph(DocumentEnglishPolicy.SignatureHeading(data.LanguageMode, customer: true), bold: true)]);
            ReplaceOptionalMarker(body, "{{SIGNATURE_PROVIDER}}",
                (_, _) => [Paragraph(DocumentEnglishPolicy.SignatureHeading(data.LanguageMode, customer: false), bold: true)]);
            var roots = new List<OpenXmlElement> { body };
            roots.AddRange(document.MainDocumentPart!.HeaderParts.Select(x => x.Header).OfType<OpenXmlElement>());
            roots.AddRange(document.MainDocumentPart.FooterParts.Select(x => x.Footer).OfType<OpenXmlElement>());
            if (document.MainDocumentPart.FootnotesPart?.Footnotes is { } footnotes) roots.Add(footnotes);
            if (document.MainDocumentPart.EndnotesPart?.Endnotes is { } endnotes) roots.Add(endnotes);
            foreach (var paragraph in roots.SelectMany(x => x.Descendants<W.Paragraph>()))
            {
                foreach (var field in AcceptanceRecordPlaceholderCatalog.All.Where(x => x.Key.EndsWith("_EN", StringComparison.Ordinal)
                             && !DocumentEnglishPolicy.IsOptionalPartyEnglish(x, data.ContractDirection)))
                    if (paragraph.InnerText.Contains($"{{{{{field.Key}}}}}", StringComparison.Ordinal))
                        DocumentEnglishPolicy.Require(values.GetValueOrDefault(field.Key), field.Label);
                ReplaceScalarTokens(paragraph, values);
            }
            foreach (var header in document.MainDocumentPart.HeaderParts) header.Header?.Save();
            foreach (var footer in document.MainDocumentPart.FooterParts) footer.Footer?.Save();
            document.MainDocumentPart.FootnotesPart?.Footnotes?.Save();
            document.MainDocumentPart.EndnotesPart?.Endnotes?.Save();
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
            if (sourceJustification is not null && paragraph.ParagraphProperties.GetFirstChild<W.Justification>() is null)
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
            if (data.LanguageMode == ContractLanguageMode.Bilingual)
                result.Add(Paragraph(DocumentEnglishPolicy.Require(reference.TextEn, "căn cứ nghiệm thu tiếng Anh"), italic: true));
            result.Add(Paragraph(reference.TextVi, italic: true));
        }
        return result.Cast<OpenXmlElement>().ToList();
    }

    private static IReadOnlyList<OpenXmlElement> CreateSectionElements(
        ContractAcceptanceDocumentRenderData data,
        int availableWidthDxa)
    {
        var result = new List<OpenXmlElement>();
        foreach (var section in data.Sections.OrderBy(x => x.DisplayOrder))
        {
            BilingualContentPolicy.Validate(section.BilingualContentMode, data.LanguageMode,
                section.TitleEn, section.ContentVi, section.ContentEn, section.SectionCode);
            result.Add(Paragraph(DocumentEnglishPolicy.Label(data.LanguageMode,
                section.TitleEn ?? string.Empty, section.TitleVi), bold: true));
            foreach (var body in DocumentEnglishPolicy.Bodies(data.LanguageMode, section.BilingualContentMode,
                         section.ContentVi, section.ContentEn))
                result.AddRange(ContentElements(body, availableWidthDxa));
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
