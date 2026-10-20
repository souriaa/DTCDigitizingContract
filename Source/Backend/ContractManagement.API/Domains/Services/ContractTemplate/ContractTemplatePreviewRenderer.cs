using ContractManagement.API.Common.Enums;
using ContractManagement.Domains.Interfaces.ContractTemplate;
using ContractManagement.Domains.Policies.ContractTemplate;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ContractManagement.Domains.Services.ContractTemplate;

/// <summary>
/// Tạo DOCX đã merge dữ liệu từ bytes của template đã validation.
/// Overload mặc định vẫn dùng dataset mẫu cho màn quản lý template; overload có renderData
/// được dùng để tạo tài liệu hợp đồng thật.
/// </summary>
public sealed class ContractTemplatePreviewRenderer : IContractTemplatePreviewRenderer
{
    public const string FormatVersion = "V22";

    private const string GeneratedContentFont = "Times New Roman";
    private const string GeneratedContentFontSize = "24";

    public byte[] RenderSample(byte[] sourceDocumentBytes, ContractLanguageMode languageMode,
        IReadOnlyList<SoftwareSupplyPlaceholderDefinition> definitions,
        IReadOnlyDictionary<string, string> customSamples,
        ContractTemplateAuthoringPreviewData? authoringData = null)
    {
        // A direction-free call is an isolated demonstration, never a persisted template.
        var direction = authoringData is null ? ContractDirection.Sale : authoringData.ContractDirection;
        ContractManagement.Domains.Policies.Contract.ContractPartyRoleResolver.Validate(direction);
        var data = CreateSampleRenderData(languageMode);
        DocumentParty SampleParty(string prefix) => new(DocumentPartyMarkers.Fields.Select(field => field.Suffix)
            .Append("REPRESENTATIVE_PHONE").ToDictionary(suffix => suffix,
                suffix => data.ScalarValues.GetValueOrDefault(prefix + "_" + suffix) ?? string.Empty));
        var sampleRoles = DocumentPartyMarkers.Values(direction, SampleParty("PROVIDER"), SampleParty("CUSTOMER"));
        var values = new Dictionary<string, string>(data.ScalarValues, StringComparer.Ordinal);
        AddAcceptanceSampleValues(values);
        values["HANDOVER_CODE"] = "BBBG-SAMPLE-001";
        values["HANDOVER_DATE"] = "19/09/2026"; values["HANDOVER_DATE_EN"] = "19 September 2026";
        values["HANDOVER_LOCATION"] = "Hà Nội"; values["HANDOVER_LOCATION_EN"] = "Hanoi";
        foreach (var pair in sampleRoles) values[pair.Key] = pair.Value;
        foreach (var pair in customSamples) values[pair.Key] = pair.Value;
        var terms = authoringData is not null
            ? authoringData.Terms
            : data.Terms;
        return Render(sourceDocumentBytes, languageMode, data with
        {
            ContractDirection = direction,
            ScalarValues = values,
            Definitions = definitions,
            DocumentReferences = [new("Căn cứ Hợp đồng số DTC-SAMPLE-001 ký ngày 01/09/2026 giữa Doanh nghiệp mẫu và Đối tác mẫu.", "Pursuant to Contract No. DTC-SAMPLE-001 signed on 01 September 2026 between Sample Tenant and Sample Counterparty.")],
            HandoverItems = [new(1, "ITEM-1", "Phần mềm mẫu", "Sample software", "Giấy phép", "License", 1)],
            ItemTableColumnWidthsBps = authoringData?.ItemTableColumnWidthsBps
                ?? data.ItemTableColumnWidthsBps,
            LegalBases = authoringData?.LegalBases ?? data.LegalBases,
            Terms = terms,
            Appendices = authoringData?.Appendices ?? data.Appendices
        });
    }

    public byte[] Render(
        byte[] sourceDocumentBytes,
        ContractLanguageMode languageMode,
        ContractTemplateRenderData renderData)
    {
        ArgumentNullException.ThrowIfNull(sourceDocumentBytes);
        ArgumentNullException.ThrowIfNull(renderData);
        ContractManagement.Domains.Policies.Contract.ContractPartyRoleResolver.Validate(renderData.ContractDirection);
        if (sourceDocumentBytes.Length == 0)
        {
            throw new ContractTemplatePreviewException(
                "PreviewSourceUnavailable",
                "Không có bytes DOCX nguồn để tạo preview.");
        }

        using var output = new MemoryStream();
        output.Write(sourceDocumentBytes, 0, sourceDocumentBytes.Length);
        output.Position = 0;

        using (var document = WordprocessingDocument.Open(output, true))
        {
            var mainPart = document.MainDocumentPart
                ?? throw new ContractTemplatePreviewException(
                    "PreviewLayoutUnsupported",
                    "DOCX nguồn không có main document để preview.");
            if (mainPart.Document?.Body is null)
            {
                throw new ContractTemplatePreviewException(
                    "PreviewLayoutUnsupported",
                    "DOCX nguồn không có body để preview.");
            }

            var definitions = renderData.Definitions
                ?? throw new ContractTemplatePreviewException(
                    "PlaceholderSnapshotMissing",
                    "Dữ liệu render không có snapshot placeholder của template version.");
            var scalarValues = new Dictionary<string, string>(renderData.ScalarValues, StringComparer.Ordinal);
            foreach (var field in definitions.Where(x => x.DataKind == TemplatePlaceholderDataKind.Scalar
                         && DocumentEnglishPolicy.IsOptionalPartyEnglish(x, renderData.ContractDirection)))
                scalarValues[field.Key] = DocumentEnglishPolicy.OptionalCustomerEnglish(scalarValues.GetValueOrDefault(field.Key));
            var dynamicParagraphs = LocateDynamicParagraphs(mainPart, definitions);
            EnsureAppendixMarkerIsLastDynamicBlock(mainPart, dynamicParagraphs);
            ValidateEnglishMarkers(mainPart, definitions, scalarValues, renderData.ContractDirection);
            ReplaceDynamicBlocks(dynamicParagraphs, languageMode, renderData);

            foreach (var root in GetTextRoots(mainPart))
            {
                ReplaceScalarTokens(root, scalarValues);
            }

            EnsureNoCatalogTokensRemain(mainPart, definitions);
            mainPart.Document.Save();
        }

        return output.ToArray();
    }

    private static void ValidateEnglishMarkers(MainDocumentPart mainPart,
        IReadOnlyList<SoftwareSupplyPlaceholderDefinition> definitions,
        IReadOnlyDictionary<string, string> values, ContractDirection direction)
    {
        var texts = GetTextRoots(mainPart).SelectMany(root => root.Descendants<W.Paragraph>())
            .Select(p => p.InnerText).ToArray();
        foreach (var field in definitions.Where(x => x.DataKind == TemplatePlaceholderDataKind.Scalar
                     && !DocumentEnglishPolicy.IsOptionalPartyEnglish(x, direction)
                     && (x.IsSystem && x.Key.EndsWith("_EN", StringComparison.Ordinal)
                         || x.SourceFieldKey?.EndsWith("-en", StringComparison.Ordinal) == true)))
            if (texts.Any(text => text.Contains($"{{{{{field.Key}}}}}", StringComparison.Ordinal)))
                DocumentEnglishPolicy.Require(values.GetValueOrDefault(field.Key), field.Label);
    }

    private static IReadOnlyDictionary<string, W.Paragraph> LocateDynamicParagraphs(
        MainDocumentPart mainPart, IReadOnlyList<SoftwareSupplyPlaceholderDefinition> definitions)
    {
        var dynamicKeys = definitions.Where(x => x.DataKind == TemplatePlaceholderDataKind.DynamicBlock).Select(x => x.Key).ToArray();
        var found = new Dictionary<string, List<W.Paragraph>>(
            StringComparer.Ordinal);
        foreach (var key in dynamicKeys)
        {
            found[key] = [];
        }

        foreach (var (root, isMainDocument) in GetTextRootsWithLocation(mainPart))
        {
            foreach (var paragraph in root.Descendants<W.Paragraph>().ToList())
            {
                var paragraphText = GetParagraphText(paragraph);
                foreach (var key in dynamicKeys)
                {
                    var token = Token(key);
                    if (!paragraphText.Contains(token, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!isMainDocument || !string.Equals(paragraphText, token,
                            StringComparison.Ordinal))
                    {
                        throw LayoutUnsupported(key);
                    }

                    found[key].Add(paragraph);
                }
            }
        }

        var result = new Dictionary<string, W.Paragraph>(StringComparer.Ordinal);
        foreach (var definition in definitions
                     .Where(item =>
                         item.DataKind == TemplatePlaceholderDataKind.DynamicBlock))
        {
            var matches = found[definition.Key];
            if (matches.Count == 0 && !definition.IsRequired)
            {
                continue;
            }

            if (matches.Count != 1)
            {
                throw LayoutUnsupported(definition.Key);
            }

            result[definition.Key] = matches[0];
        }

        return result;
    }

    private static void EnsureAppendixMarkerIsLastDynamicBlock(
        MainDocumentPart mainPart,
        IReadOnlyDictionary<string, W.Paragraph> paragraphs)
    {
        if (!paragraphs.TryGetValue("CONTRACT_APPENDICES", out var appendix))
        {
            return;
        }

        var bodyParagraphs = mainPart.Document!.Body!
            .Descendants<W.Paragraph>()
            .ToList();
        var appendixIndex = bodyParagraphs.IndexOf(appendix);
        var laterDynamicBlockExists = paragraphs
            .Where(item => item.Key != "CONTRACT_APPENDICES")
            .Any(item => bodyParagraphs.IndexOf(item.Value) > appendixIndex);
        if (appendixIndex < 0 || laterDynamicBlockExists)
        {
            throw new ContractTemplatePreviewException(
                "AppendixMarkerPositionInvalid",
                "CONTRACT_APPENDICES phải là block động cuối cùng, sau toàn bộ nội dung hợp đồng gốc.");
        }
    }

    private static void ReplaceDynamicBlocks(
        IReadOnlyDictionary<string, W.Paragraph> paragraphs,
        ContractLanguageMode languageMode,
        ContractTemplateRenderData renderData)
    {
        foreach (var (key, paragraph) in paragraphs)
        {
            var availableWidthDxa = GetAvailableWidthDxa(paragraph);
            IEnumerable<OpenXmlElement> replacements = key switch
            {
                "CONTRACT_ITEM_TABLE" =>
                [
                    (OpenXmlElement)CreateItemTable(
                        languageMode, renderData, availableWidthDxa)
                ],
                "CONTRACT_LEGAL_BASES" => CreateLegalBasisElements(
                    languageMode, renderData, availableWidthDxa),
                "CONTRACT_TERMS" => CreateTermElements(
                    languageMode, renderData, availableWidthDxa),
                "CONTRACT_APPENDICES" => CreateAppendixElements(
                    languageMode, renderData, availableWidthDxa),
                "DOCUMENT_REFERENCES" => CreateDocumentReferenceElements(languageMode, renderData.DocumentReferences),
                "HANDOVER_ITEM_TABLE" => [CreateHandoverItemTable(languageMode, renderData.HandoverItems, availableWidthDxa)],
                "HANDOVER_SECTIONS" => CreateAcceptanceSectionElements(languageMode, renderData.Terms, availableWidthDxa),
                "ACCEPTANCE_SECTIONS" => CreateAcceptanceSectionElements(
                    languageMode, renderData.Terms, availableWidthDxa),
                "SIGNATURE_PROVIDER" =>
                [
                    (OpenXmlElement)CreateStyledParagraph(
                        DocumentEnglishPolicy.SignatureHeading(languageMode, customer: false), bold: true)
                ],
                "SIGNATURE_CUSTOMER" =>
                [
                    (OpenXmlElement)CreateStyledParagraph(
                        DocumentEnglishPolicy.SignatureHeading(languageMode, customer: true), bold: true)
                ],
                _ => throw LayoutUnsupported(key)
            };

            ReplaceParagraph(paragraph, replacements);
        }
    }

    private static IReadOnlyList<OpenXmlElement> CreateDocumentReferenceElements(
        ContractLanguageMode languageMode, IReadOnlyList<ContractTemplateRenderReference> references)
    {
        var result = new List<OpenXmlElement>();
        foreach (var reference in references)
        {
            if (languageMode == ContractLanguageMode.Bilingual)
                result.Add(CreateStyledParagraph(DocumentEnglishPolicy.Require(reference.TextEn, "căn cứ tiếng Anh"), italic: true));
            result.Add(CreateStyledParagraph(reference.TextVi, italic: true));
        }
        return result;
    }

    private static W.Table CreateHandoverItemTable(ContractLanguageMode language,
        IReadOnlyList<ContractTemplateRenderHandoverItem> items, int width)
    {
        var widths = ContractTableLayoutPolicy.MapToDxa([700, 1500, 4600, 1400, 1800], width);
        var rows = new List<IEnumerable<string>> { new[] {
            DocumentEnglishPolicy.Label(language, "No.", "STT"), DocumentEnglishPolicy.Label(language, "Code", "Mã"),
            DocumentEnglishPolicy.Label(language, "Delivered items", "Hạng mục bàn giao"),
            DocumentEnglishPolicy.Label(language, "Unit", "Đơn vị"), DocumentEnglishPolicy.Label(language, "Quantity", "Số lượng") } };
        foreach (var item in items)
        {
            var name = language == ContractLanguageMode.Bilingual
                ? DocumentEnglishPolicy.Require(item.NameEn, "tên hạng mục tiếng Anh") + "\n\n" + item.NameVi : item.NameVi;
            var unit = language == ContractLanguageMode.Bilingual
                ? DocumentEnglishPolicy.Require(item.UnitEn, "đơn vị tiếng Anh") + "\n\n" + item.UnitVi : item.UnitVi;
            if (!string.IsNullOrWhiteSpace(item.NotesVi) || !string.IsNullOrWhiteSpace(item.NotesEn))
                name += "\n" + (language == ContractLanguageMode.Bilingual ? item.NotesEn + "\n\n" : "") + item.NotesVi;
            rows.Add(new[] { item.No.ToString(), item.Code, name, unit, item.Quantity.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) });
        }
        return CreateTable(rows, true, widths, width);
    }

    private static IReadOnlyList<OpenXmlElement> CreateAcceptanceSectionElements(
        ContractLanguageMode languageMode,
        IReadOnlyList<ContractTemplateRenderTerm> sections,
        int availableWidthDxa)
    {
        if (sections.Count == 0)
        {
            return [];
        }

        var result = new List<OpenXmlElement>();
        foreach (var section in sections)
        {
            result.Add(CreateStyledParagraph(DocumentEnglishPolicy.Label(languageMode,
                section.TitleEn, section.TitleVi), bold: true));
            foreach (var body in DocumentEnglishPolicy.Bodies(languageMode, section.BilingualContentMode,
                         section.ContentVi, section.ContentEn))
                result.AddRange(CreateTermContentElements(body, availableWidthDxa));
        }

        return result;
    }

    private static void ReplaceParagraph(
        W.Paragraph paragraph,
        IEnumerable<OpenXmlElement> replacements)
    {
        var parent = paragraph.Parent as OpenXmlCompositeElement
            ?? throw new ContractTemplatePreviewException(
                "PreviewLayoutUnsupported",
                "Placeholder động không có parent hợp lệ để thay thế.");
        var materialized = replacements.ToList();
        foreach (var replacement in materialized)
        {
            ApplyPlaceholderFormatting(paragraph, replacement);
            parent.InsertBefore(replacement, paragraph);
        }

        paragraph.Remove();
        // Word requires a table cell to end with a paragraph, including when
        // an appendix block ends with its signature table.
        if (parent is W.TableCell
            && (materialized.Count == 0 || materialized[^1] is not W.Paragraph))
        {
            parent.Append(new W.Paragraph());
        }
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

    internal static int GetAvailableWidthDxa(W.Paragraph paragraph)
    {
        var containingCell = paragraph.Ancestors<W.TableCell>().FirstOrDefault();
        if (containingCell is not null)
        {
            var explicitWidth = containingCell.TableCellProperties?
                .GetFirstChild<W.TableCellWidth>();
            if (explicitWidth?.Type?.Value == W.TableWidthUnitValues.Dxa
                && int.TryParse(explicitWidth.Width?.Value, out var cellWidth)
                && cellWidth > 0)
            {
                return cellWidth;
            }

            var row = containingCell.Parent as W.TableRow;
            var table = containingCell.Ancestors<W.Table>().FirstOrDefault();
            var gridWidths = table?.GetFirstChild<W.TableGrid>()?
                .Elements<W.GridColumn>()
                .Select(column => int.TryParse(column.Width?.Value, out var width)
                    ? width
                    : 0)
                .ToArray();
            if (row is not null && gridWidths is { Length: > 0 })
            {
                var startColumn = row.Elements<W.TableCell>()
                    .TakeWhile(cell => !ReferenceEquals(cell, containingCell))
                    .Sum(cell => (int?)(cell.TableCellProperties?
                        .GetFirstChild<W.GridSpan>()?.Val?.Value) ?? 1);
                var colspan = (int?)(containingCell.TableCellProperties?
                    .GetFirstChild<W.GridSpan>()?.Val?.Value) ?? 1;
                var gridWidth = gridWidths.Skip(startColumn).Take(colspan).Sum();
                if (gridWidth > 0) return gridWidth;
            }
        }

        var body = paragraph.Ancestors<W.Body>().FirstOrDefault();
        var section = body?.Descendants<W.SectionProperties>().LastOrDefault();
        var pageSize = section?.GetFirstChild<W.PageSize>();
        var pageMargin = section?.GetFirstChild<W.PageMargin>();
        var pageWidth = checked((int)(pageSize?.Width?.Value ?? 12_240U));
        var leftMargin = checked((int)(pageMargin?.Left?.Value ?? 1_440U));
        var rightMargin = checked((int)(pageMargin?.Right?.Value ?? 1_440U));
        var available = pageWidth - leftMargin - rightMargin;
        if (available <= 0)
        {
            throw new ContractTemplatePreviewException(
                "PreviewLayoutUnsupported",
                "Không xác định được chiều rộng vùng nội dung của DOCX.");
        }

        return available;
    }

    private static IEnumerable<OpenXmlElement> CreateTermElements(
        ContractLanguageMode languageMode,
        ContractTemplateRenderData renderData,
        int availableWidthDxa)
    {
        var elements = new List<OpenXmlElement>();
        if (!string.IsNullOrWhiteSpace(renderData.Notice))
        {
            elements.Add(CreateParagraph(renderData.Notice, bold: true));
        }

        foreach (var term in renderData.Terms)
        {
            var title = languageMode == ContractLanguageMode.Bilingual
                ? $"Article {term.No}. {term.TitleEn} / Điều {term.No}. {term.TitleVi}"
                : $"Điều {term.No}. {term.TitleVi}";
            elements.Add(CreateParagraph(title, bold: true));
            foreach (var body in DocumentEnglishPolicy.Bodies(languageMode, term.BilingualContentMode,
                         term.ContentVi, term.ContentEn))
                elements.AddRange(CreateTermContentElements(body, availableWidthDxa));
        }

        return elements;
    }

    private static IEnumerable<OpenXmlElement> CreateAppendixElements(
        ContractLanguageMode languageMode,
        ContractTemplateRenderData renderData,
        int availableWidthDxa)
    {
        var elements = new List<OpenXmlElement>();
        foreach (var appendix in renderData.Appendices)
        {
            if (languageMode == ContractLanguageMode.Bilingual)
            {
                DocumentEnglishPolicy.Require(appendix.NameEn, "tên phụ lục tiếng Anh");
                DocumentEnglishPolicy.Require(appendix.ReferenceEn, "căn cứ phụ lục tiếng Anh");
            }
            elements.Add(new W.Paragraph(
                new W.Run(CreateGeneratedRunProperties(),
                    new W.Break { Type = W.BreakValues.Page })));
            var title = languageMode == ContractLanguageMode.Bilingual
                && !string.IsNullOrWhiteSpace(appendix.NameEn)
                ? $"APPENDIX {appendix.Code}: {appendix.NameEn} / PHỤ LỤC {appendix.Code}: {appendix.NameVi}"
                : $"PHỤ LỤC {appendix.Code}: {appendix.NameVi}";
            elements.Add(CreateStyledParagraph(title, bold: true,
                justification: W.JustificationValues.Center));
            if (languageMode == ContractLanguageMode.Bilingual
                && !string.IsNullOrWhiteSpace(appendix.ReferenceEn))
            {
                elements.Add(CreateStyledParagraph(appendix.ReferenceEn!,
                    italic: true));
            }

            elements.Add(CreateStyledParagraph(appendix.ReferenceVi, italic: true));

            foreach (var term in appendix.Terms)
            {
                var termTitle = languageMode == ContractLanguageMode.Bilingual
                    && !string.IsNullOrWhiteSpace(term.TitleEn)
                    ? $"Article {term.No}. {term.TitleEn} / Điều {term.No}. {term.TitleVi}"
                    : $"Điều {term.No}. {term.TitleVi}";
                elements.Add(CreateParagraph(termTitle, bold: true));
                foreach (var body in DocumentEnglishPolicy.Bodies(languageMode, term.BilingualContentMode,
                             term.ContentVi, term.ContentEn))
                    elements.AddRange(CreateRichTextElements(body, "ContractAppendixTermRichTextInvalid",
                        "Nội dung điều khoản phụ lục không hợp lệ.", availableWidthDxa));
            }

            elements.Add(CreateAppendixSignatureTable(availableWidthDxa, languageMode));
        }

        return elements;
    }

    private static W.Table CreateAppendixSignatureTable(int availableWidthDxa, ContractLanguageMode languageMode)
    {
        var columnWidths = new[]
        {
            availableWidthDxa / 2,
            availableWidthDxa - availableWidthDxa / 2
        };
        var table = new W.Table(
            new W.TableProperties(
                new W.TableWidth
                {
                    Type = W.TableWidthUnitValues.Dxa,
                    Width = availableWidthDxa.ToString()
                },
                new W.TableLayout { Type = W.TableLayoutValues.Fixed },
                new W.TableBorders(
                    new W.TopBorder { Val = W.BorderValues.Nil },
                    new W.LeftBorder { Val = W.BorderValues.Nil },
                    new W.BottomBorder { Val = W.BorderValues.Nil },
                    new W.RightBorder { Val = W.BorderValues.Nil },
                    new W.InsideHorizontalBorder { Val = W.BorderValues.Nil },
                    new W.InsideVerticalBorder { Val = W.BorderValues.Nil })));
        table.Append(new W.TableGrid(
            new W.GridColumn { Width = columnWidths[0].ToString() },
            new W.GridColumn { Width = columnWidths[1].ToString() }));
        table.Append(new W.TableRow(
            new W.TableCell(
                CreateFixedCellProperties(columnWidths, 0, 1),
                CreateStyledParagraph(DocumentEnglishPolicy.SignatureHeading(languageMode, customer: true),
                    bold: true, justification: W.JustificationValues.Center)),
            new W.TableCell(
                CreateFixedCellProperties(columnWidths, 1, 1),
                CreateStyledParagraph(DocumentEnglishPolicy.SignatureHeading(languageMode, customer: false),
                    bold: true, justification: W.JustificationValues.Center))));
        return table;
    }

    private static IEnumerable<OpenXmlElement> CreateLegalBasisElements(
        ContractLanguageMode languageMode,
        ContractTemplateRenderData renderData,
        int availableWidthDxa)
    {
        var elements = new List<OpenXmlElement>();
        foreach (var basis in renderData.LegalBases)
        {
            foreach (var body in DocumentEnglishPolicy.Bodies(languageMode, (byte)BilingualContentMode.Separate,
                         basis.ContentVi, basis.ContentEn))
                elements.AddRange(CreateRichTextElements(body, "ContractLegalBasisRichTextInvalid",
                    "Nội dung căn cứ có định dạng rich text không hợp lệ.", availableWidthDxa));
        }

        return elements;
    }

    private static IEnumerable<OpenXmlElement> CreateTermContentElements(
        string? value,
        int availableWidthDxa)
        => CreateRichTextElements(
            value,
            "ContractTermRichTextInvalid",
            "Nội dung điều khoản có định dạng rich text không hợp lệ.",
            availableWidthDxa);

    internal static IEnumerable<OpenXmlElement> CreateRichTextElements(
        string? value,
        string failureCode,
        string failureMessage,
        int availableWidthDxa)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [CreateParagraph(string.Empty)];
        }

        if (!ContractTermRichText.TryParse(value, out var document))
        {
            throw new ContractTemplatePreviewException(
                failureCode,
                failureMessage);
        }

        var elements = new List<OpenXmlElement>();
        foreach (var block in document.Blocks)
        {
            if (block.Type == "paragraph")
            {
                elements.Add(CreateRichParagraph(block.Runs, block.Alignment));
                continue;
            }

            elements.Add(CreateRichTable(
                block.Rows,
                block.ColumnWidthsBps,
                availableWidthDxa));
        }

        if (elements.Count == 0)
        {
            elements.Add(CreateParagraph(string.Empty));
        }

        return elements;
    }

    private static W.Paragraph CreateRichParagraph(
        IEnumerable<ContractTermRichTextRun> runs,
        string? alignment = null)
    {
        var paragraph = new W.Paragraph();
        if (ToJustification(alignment) is { } justification)
        {
            paragraph.ParagraphProperties = new W.ParagraphProperties(
                new W.Justification { Val = justification });
        }
        foreach (var run in runs)
        {
            paragraph.Append(CreateRichRun(run));
        }

        return paragraph;
    }

    private static W.Run CreateRichRun(ContractTermRichTextRun value)
    {
        var run = new W.Run(CreateGeneratedRunProperties(
            bold: value.Bold,
            fontSize: value.FontSize is null
                ? null
                : (value.FontSize.Value * 2).ToString(),
            italic: value.Italic,
            underline: value.Underline));
        var parts = value.Text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n');
        for (var index = 0; index < parts.Length; index++)
        {
            if (index > 0) run.Append(new W.Break());
            if (parts[index].Length > 0) run.Append(Text(parts[index]));
        }

        return run;
    }

    private static W.Table CreateRichTable(
        IEnumerable<ContractTermRichTextRow> rows,
        IReadOnlyList<int> columnWidthsBps,
        int availableWidthDxa)
    {
        var layout = CreateRichTableLayout(rows.ToList());
        var widths = ContractTableLayoutPolicy.MapToDxa(
            columnWidthsBps, availableWidthDxa);
        var table = CreateFixedTable(widths, availableWidthDxa);

        foreach (var sourceRow in layout.Rows)
        {
            var row = new W.TableRow();
            foreach (var layoutCell in sourceRow)
            {
                var properties = CreateFixedCellProperties(
                    widths, layoutCell.StartColumn, layoutCell.Colspan);
                if (layoutCell.IsVerticalContinuation)
                {
                    properties.Append(new W.VerticalMerge
                    {
                        Val = W.MergedCellValues.Continue
                    });
                }
                else if ((layoutCell.Cell.Rowspan ?? 1) > 1)
                {
                    properties.Append(new W.VerticalMerge
                    {
                        Val = W.MergedCellValues.Restart
                    });
                }
                if (ToTableVerticalAlignment(layoutCell.Cell.VerticalAlign) is { } verticalAlignment)
                {
                    properties.Append(new W.TableCellVerticalAlignment
                    {
                        Val = verticalAlignment
                    });
                }

                var tableCell = new W.TableCell(properties);
                if (layoutCell.IsVerticalContinuation)
                {
                    tableCell.Append(new W.Paragraph());
                }
                else
                {
                    var cell = layoutCell.Cell;
                    var paragraphs = cell.Paragraphs.Select(paragraph =>
                        CreateRichParagraph(paragraph.Runs, paragraph.Alignment));
                    tableCell.Append(paragraphs);
                }
                row.Append(tableCell);
            }

            table.Append(row);
        }

        return table;
    }

    private static RichTableLayout CreateRichTableLayout(
        IReadOnlyList<ContractTermRichTextRow> rows)
    {
        var columnCount = rows[0].Cells.Sum(cell => cell.Colspan ?? 1);
        var activeSpans = new List<ActiveRichTableSpan>();
        var layoutRows = new List<IReadOnlyList<RichTableLayoutCell>>(rows.Count);

        foreach (var sourceRow in rows)
        {
            var occupied = new bool[columnCount];
            var layoutCells = new List<RichTableLayoutCell>();
            var nextActiveSpans = new List<ActiveRichTableSpan>();
            foreach (var span in activeSpans)
            {
                for (var column = span.StartColumn;
                     column < span.StartColumn + span.Colspan;
                     column++)
                {
                    occupied[column] = true;
                }
                layoutCells.Add(new RichTableLayoutCell(
                    span.Cell,
                    span.StartColumn,
                    span.Colspan,
                    true));
                if (span.RemainingRows > 1)
                {
                    nextActiveSpans.Add(span with
                    {
                        RemainingRows = span.RemainingRows - 1
                    });
                }
            }

            var cursor = 0;
            foreach (var cell in sourceRow.Cells)
            {
                while (occupied[cursor]) cursor++;
                var colspan = cell.Colspan ?? 1;
                layoutCells.Add(new RichTableLayoutCell(cell, cursor, colspan, false));
                for (var column = cursor; column < cursor + colspan; column++)
                {
                    occupied[column] = true;
                }
                if ((cell.Rowspan ?? 1) > 1)
                {
                    nextActiveSpans.Add(new ActiveRichTableSpan(
                        cell,
                        cursor,
                        colspan,
                        cell.Rowspan!.Value - 1));
                }
                cursor += colspan;
            }

            layoutRows.Add(layoutCells.OrderBy(cell => cell.StartColumn).ToList());
            activeSpans = nextActiveSpans;
        }

        return new RichTableLayout(layoutRows);
    }

    private static W.TableVerticalAlignmentValues? ToTableVerticalAlignment(
        string? alignment) => alignment switch
        {
            "top" => W.TableVerticalAlignmentValues.Top,
            "center" => W.TableVerticalAlignmentValues.Center,
            "bottom" => W.TableVerticalAlignmentValues.Bottom,
            _ => null
        };

    private sealed record RichTableLayout(
        IReadOnlyList<IReadOnlyList<RichTableLayoutCell>> Rows);

    private sealed record RichTableLayoutCell(
        ContractTermRichTextCell Cell,
        int StartColumn,
        int Colspan,
        bool IsVerticalContinuation);

    private sealed record ActiveRichTableSpan(
        ContractTermRichTextCell Cell,
        int StartColumn,
        int Colspan,
        int RemainingRows);

    private static W.JustificationValues? ToJustification(string? alignment) =>
        alignment switch
        {
            "center" => W.JustificationValues.Center,
            "right" => W.JustificationValues.Right,
            "left" => W.JustificationValues.Left,
            _ => null
        };

    private static W.Table CreateItemTable(
        ContractLanguageMode languageMode,
        ContractTemplateRenderData renderData,
        int availableWidthDxa)
    {
        var headers = languageMode == ContractLanguageMode.Bilingual
            ? new[] { "No.\n\nSTT", "Type\n\nLoại", "Description\n\nSản phẩm, dịch vụ", "Qty\n\nSL", "Unit price\n\nĐơn giá", "Disc.\n\nCK", "VAT", "Total\n\nThành tiền" }
            : new[] { "STT", "Loại", "Sản phẩm, dịch vụ", "SL", "Đơn giá", "CK", "VAT", "Thành tiền" };
        var rows = new List<IEnumerable<string>>
        {
            headers
        };

        rows.AddRange(renderData.Items.Select(item =>
            (IEnumerable<string>)
            [
                item.No.ToString(),
                RenderItemLanguages(item.Type, item.TypeEn, languageMode),
                RenderItemLanguages(item.Description, item.DescriptionEn, languageMode),
                FormatQuantity(item.Quantity),
                FormatMoney(item.UnitPrice, renderData.CurrencyCode),
                item.Discount,
                item.Vat,
                FormatMoney(item.TotalAmount, renderData.CurrencyCode)
            ]));

        var columnWidths = ContractTableLayoutPolicy.MapToDxa(
            ContractTableLayoutPolicy.RequireCanonicalWidths(
                renderData.ItemTableColumnWidthsBps,
                ContractTableLayoutPolicy.ItemColumnKeys.Count,
                "ItemTableLayoutInvalid"),
            availableWidthDxa);
        var table = CreateTable(
            rows,
            headerRow: true,
            columnWidths,
            availableWidthDxa);
        var labelColumnCount = columnWidths.Count - 1;
        var totalLabel = CreateParagraph(languageMode == ContractLanguageMode.Bilingual
            ? "TOTAL\n\nTỔNG CỘNG" : "TỔNG CỘNG", bold: true);
        totalLabel.ParagraphProperties = new W.ParagraphProperties(
            new W.Justification { Val = W.JustificationValues.Right });
        var totalRow = new W.TableRow(
            new W.TableCell(
                CreateFixedCellProperties(columnWidths, 0, labelColumnCount),
                totalLabel),
            new W.TableCell(
                CreateFixedCellProperties(columnWidths, labelColumnCount, 1),
                CreateParagraph(FormatMoney(
                    renderData.Items.Sum(item => item.TotalAmount),
                    renderData.CurrencyCode))));
        table.Append(totalRow);
        return table;
    }

    private static string RenderItemLanguages(string vietnamese, string? english,
        ContractLanguageMode languageMode)
    {
        if (languageMode != ContractLanguageMode.Bilingual) return vietnamese;
        if (string.IsNullOrWhiteSpace(english))
            throw new ContractTemplatePreviewException("BilingualItemDataRequired",
                "Sản phẩm/dịch vụ trong tài liệu song ngữ thiếu nội dung tiếng Anh.");
        return $"{english}\n\n{vietnamese}";
    }

    private static W.Table CreateTable(
        IEnumerable<IEnumerable<string>> rows,
        bool headerRow,
        IReadOnlyList<int> columnWidths,
        int availableWidthDxa)
    {
        var table = CreateFixedTable(columnWidths, availableWidthDxa);

        var rowIndex = 0;
        foreach (var values in rows)
        {
            var row = new W.TableRow();
            foreach (var (value, columnIndex) in values.Select(
                         (value, columnIndex) => (value, columnIndex)))
            {
                var paragraph = CreateParagraph(value, bold: headerRow && rowIndex == 0);
                row.Append(new W.TableCell(
                    CreateFixedCellProperties(columnWidths, columnIndex, 1),
                    paragraph));
            }

            table.Append(row);
            rowIndex++;
        }

        return table;
    }

    private static W.Table CreateFixedTable(
        IReadOnlyList<int> columnWidths,
        int availableWidthDxa)
    {
        var table = new W.Table(CreateGeneratedTableProperties(availableWidthDxa));
        var grid = new W.TableGrid();
        foreach (var width in columnWidths)
            grid.Append(new W.GridColumn { Width = width.ToString() });
        table.Append(grid);
        return table;
    }

    private static W.TableCellProperties CreateFixedCellProperties(
        IReadOnlyList<int> columnWidths,
        int startColumn,
        int colspan)
    {
        var properties = new W.TableCellProperties(
            new W.TableCellWidth
            {
                Type = W.TableWidthUnitValues.Dxa,
                Width = columnWidths
                    .Skip(startColumn)
                    .Take(colspan)
                    .Sum()
                    .ToString()
            });
        if (colspan > 1) properties.Append(new W.GridSpan { Val = colspan });
        return properties;
    }

    private static W.TableProperties CreateGeneratedTableProperties(
        int availableWidthDxa) =>
        new(
            new W.TableWidth
            {
                Type = W.TableWidthUnitValues.Dxa,
                Width = availableWidthDxa.ToString()
            },
            new W.TableBorders(
                new W.TopBorder { Val = W.BorderValues.Single, Size = 4 },
                new W.LeftBorder { Val = W.BorderValues.Single, Size = 4 },
                new W.BottomBorder { Val = W.BorderValues.Single, Size = 4 },
                new W.RightBorder { Val = W.BorderValues.Single, Size = 4 },
                new W.InsideHorizontalBorder { Val = W.BorderValues.Single, Size = 4 },
                new W.InsideVerticalBorder { Val = W.BorderValues.Single, Size = 4 }),
            new W.TableLayout { Type = W.TableLayoutValues.Fixed },
            new W.TableCellMarginDefault(
                new W.TopMargin
                {
                    Type = W.TableWidthUnitValues.Dxa,
                    Width = "50"
                },
                new W.TableCellLeftMargin
                {
                    Type = W.TableWidthValues.Dxa,
                    Width = 80
                },
                new W.BottomMargin
                {
                    Type = W.TableWidthUnitValues.Dxa,
                    Width = "50"
                },
                new W.TableCellRightMargin
                {
                    Type = W.TableWidthValues.Dxa,
                    Width = 80
                }));

    private static void ReplaceScalarTokens(
        OpenXmlPartRootElement root,
        IReadOnlyDictionary<string, string> values)
    {
        foreach (var paragraph in root.Descendants<W.Paragraph>().ToList())
        {
            var textElements = paragraph.Descendants<W.Text>().ToList();
            if (textElements.Count == 0)
            {
                continue;
            }

            var before = string.Concat(textElements.Select(text => text.Text));
            // Match only original tokens once; values are literal text, never another substitution pass.
            var after = System.Text.RegularExpressions.Regex.Replace(before,
                @"\{\{([A-Z][A-Z0-9]*(?:_[A-Z0-9]+)*)\}\}",
                match => values.TryGetValue(match.Groups[1].Value, out var value) ? value : match.Value);

            if (string.Equals(before, after, StringComparison.Ordinal))
            {
                continue;
            }

            // Placeholder can be split between Word runs. Collapsing only the
            // affected paragraph guarantees replacement across those runs.
            textElements[0].Text = after;
            textElements[0].Space = SpaceProcessingModeValues.Preserve;
            foreach (var text in textElements.Skip(1))
            {
                text.Text = string.Empty;
            }
        }
    }

    private static void EnsureNoCatalogTokensRemain(MainDocumentPart mainPart, IReadOnlyList<SoftwareSupplyPlaceholderDefinition> definitions)
    {
        var text = string.Concat(GetTextRoots(mainPart)
            .SelectMany(root => root.Descendants<W.Text>())
            .Select(value => value.Text));
        var remaining = definitions
            .Select(definition => Token(definition.Key))
            .FirstOrDefault(token => text.Contains(token, StringComparison.Ordinal));
        if (remaining is not null || System.Text.RegularExpressions.Regex.IsMatch(text, @"\{\{[^{}]*\}\}"))
        {
            throw new ContractTemplatePreviewException(
                "PreviewRenderIncomplete",
                "Preview DOCX còn placeholder catalog chưa được thay thế.");
        }
    }

    private static IEnumerable<OpenXmlPartRootElement> GetTextRoots(
        MainDocumentPart mainPart) => GetTextRootsWithLocation(mainPart)
            .Select(item => item.Root);

    private static IEnumerable<(OpenXmlPartRootElement Root, bool IsMainDocument)>
        GetTextRootsWithLocation(MainDocumentPart mainPart)
    {
        if (mainPart.Document is not null)
        {
            yield return (mainPart.Document, true);
        }

        foreach (var header in mainPart.HeaderParts)
        {
            if (header.Header is not null)
            {
                yield return (header.Header, false);
            }
        }

        foreach (var footer in mainPart.FooterParts)
        {
            if (footer.Footer is not null)
            {
                yield return (footer.Footer, false);
            }
        }

        if (mainPart.FootnotesPart?.Footnotes is not null)
        {
            yield return (mainPart.FootnotesPart.Footnotes, false);
        }

        if (mainPart.EndnotesPart?.Endnotes is not null)
        {
            yield return (mainPart.EndnotesPart.Endnotes, false);
        }
    }

    private static string GetParagraphText(W.Paragraph paragraph) =>
        string.Concat(paragraph.Descendants<W.Text>().Select(text => text.Text));

    private static W.Paragraph CreateParagraph(string value, bool bold = false)
    {
        var paragraph = new W.Paragraph();
        foreach (var (line, index) in value.Split('\n').Select((line, index) => (line, index)))
        {
            if (index > 0) paragraph.Append(CreateBreakRun());
            paragraph.Append(CreateRun(line, bold));
        }
        return paragraph;
    }

    private static W.Paragraph CreateStyledParagraph(
        string value,
        bool bold = false,
        bool italic = false,
        W.JustificationValues? justification = null)
    {
        var paragraph = new W.Paragraph(
            new W.Run(CreateGeneratedRunProperties(
                bold: bold, italic: italic), Text(value)));
        if (justification.HasValue)
        {
            paragraph.ParagraphProperties = new W.ParagraphProperties(
                new W.Justification { Val = justification.Value });
        }
        return paragraph;
    }

    private static W.Run CreateRun(string value, bool bold = false) =>
        new(CreateGeneratedRunProperties(bold), Text(value));

    private static W.Run CreateBreakRun() =>
        new(CreateGeneratedRunProperties(), new W.Break());

    private static W.RunProperties CreateGeneratedRunProperties(
        bool bold = false,
        string? fontSize = null,
        bool italic = false,
        bool underline = false) =>
        new(
            new W.RunFonts
            {
                Ascii = GeneratedContentFont,
                HighAnsi = GeneratedContentFont,
                EastAsia = GeneratedContentFont,
                ComplexScript = GeneratedContentFont
            },
            new W.Bold { Val = bold },
            new W.Italic { Val = italic },
            new W.FontSize { Val = fontSize ?? GeneratedContentFontSize },
            new W.FontSizeComplexScript { Val = fontSize ?? GeneratedContentFontSize },
            new W.Underline
            {
                Val = underline ? W.UnderlineValues.Single : W.UnderlineValues.None
            });

    private static W.Text Text(string value) => new(value)
    {
        Space = SpaceProcessingModeValues.Preserve
    };

    private static string Token(string key) => $"{{{{{key}}}}}";

    private static ContractTemplateRenderData CreateSampleRenderData(
        ContractLanguageMode languageMode) => new(
        SoftwareSupplyPreviewDatasetV1.GetScalarValues(languageMode),
        SoftwareSupplyPreviewDatasetV1.Items.Select(item =>
            new ContractTemplateRenderItem(
                item.No,
                item.Type,
                item.Description,
                item.Quantity,
                item.UnitPrice,
                $"{item.DiscountPercent:0}%",
                $"{item.VatPercent:0}%",
                item.TotalAmount)
            {
                TypeEn = item.No == 1 ? "Product" : "Service",
                DescriptionEn = item.No == 1
                    ? "Sample enterprise contract management platform with approval workflows and an extended template library"
                    : "Sample deployment, configuration and remote operation support services including key-user training"
            }).ToList(),
        SoftwareSupplyPreviewDatasetV1.Terms.Select(term =>
            new ContractTemplateRenderTerm(
                term.No,
                term.TitleVi,
                term.TitleEn,
                term.ContentVi,
                term.ContentEn)).ToList(),
        new ContractTemplateRenderSignature(
            SoftwareSupplyPreviewDatasetV1.ProviderSignature.PartyTitle,
            SoftwareSupplyPreviewDatasetV1.ProviderSignature.SignerName),
        new ContractTemplateRenderSignature(
            SoftwareSupplyPreviewDatasetV1.CustomerSignature.PartyTitle,
            SoftwareSupplyPreviewDatasetV1.CustomerSignature.SignerName),
        DocumentEnglishPolicy.Label(languageMode, "SAMPLE DATA — NO LEGAL VALUE", SoftwareSupplyPreviewDatasetV1.LegalDisclaimer))
        {
            Definitions = ContractPlaceholderCatalog.SystemDefinitions,
            ItemTableColumnWidthsBps =
                ContractTableLayoutPolicy.DefaultItemColumnWidthsBps,
            LegalBases = SoftwareSupplyPreviewDatasetV1.LegalBases.Select(basis =>
                new ContractTemplateRenderLegalBasis(
                    basis.No, basis.ContentVi, basis.ContentEn)).ToList()
        };

    private static void AddAcceptanceSampleValues(
        IDictionary<string, string> values)
    {
        values["ACCEPTANCE_DATE_EN"] = "20 September 2026";
        values["ACCEPTANCE_LOCATION_EN"] = "Hanoi";
        values["ACCEPTANCE_CODE"] = "BBNT-SAMPLE-001";
        values["ACCEPTANCE_DATE"] = "20/09/2026";
        values["ACCEPTANCE_LOCATION"] = "Hà Nội";
    }

    private static ContractTemplatePreviewException LayoutUnsupported(string key) =>
        new(
            "PreviewLayoutUnsupported",
            $"Placeholder động {key} phải đứng một mình trong paragraph của main document.");

    private static string FormatMoney(decimal value, string currencyCode)
    {
        var decimals = string.Equals(currencyCode, "VND",
            StringComparison.OrdinalIgnoreCase) ? 0 : 2;
        var culture = System.Globalization.CultureInfo.GetCultureInfo(
            decimals == 0 ? "vi-VN" : "en-US");
        return $"{value.ToString(decimals == 0 ? "N0" : "N2", culture)} {currencyCode}";
    }

    private static string FormatQuantity(decimal value) =>
        value == decimal.Truncate(value) ? value.ToString("0") : value.ToString("0.##");

}
