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
    IReadOnlyList<ContractAcceptanceSectionResponse> Sections);

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
            ReplaceRequiredMarker(body, ReferencesMarker,
                (_, _) => CreateReferenceParagraphs(data));
            ReplaceRequiredMarker(body, SectionsMarker,
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
            foreach (var paragraph in body.Descendants<W.Paragraph>())
                ReplaceScalarTokens(paragraph, values);
            document.MainDocumentPart!.Document.Save();
        }
        return output.ToArray();
    }

    private static void ReplaceRequiredMarker(
        W.Body body,
        string marker,
        Func<W.Paragraph, int, IReadOnlyList<OpenXmlElement>> createReplacements)
    {
        var matches = body.Descendants<W.Paragraph>()
            .Where(paragraph => string.Equals(
                paragraph.InnerText.Trim(), marker, StringComparison.Ordinal))
            .ToList();
        if (matches.Count != 1)
            throw new InvalidOperationException(
                $"Acceptance template phải có đúng một marker {marker}.");
        var target = matches[0];
        var replacements = createReplacements(target,
            ContractTemplatePreviewRenderer.GetAvailableWidthDxa(target));
        foreach (var paragraph in replacements)
            target.InsertBeforeSelf(paragraph);
        target.Remove();
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
        var text = paragraph.InnerText;
        var replaced = text;
        foreach (var (key, value) in values)
            replaced = replaced.Replace(
                $"{{{{{key}}}}}", value, StringComparison.Ordinal);
        if (replaced == text) return;
        paragraph.RemoveAllChildren<W.Run>();
        paragraph.Append(new W.Run(new W.Text(replaced)
        {
            Space = SpaceProcessingModeValues.Preserve
        }));
    }
}
