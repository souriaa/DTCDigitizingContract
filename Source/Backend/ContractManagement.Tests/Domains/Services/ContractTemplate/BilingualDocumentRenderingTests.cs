using System.Globalization;
using ContractManagement.API.Common.Enums;
using ContractManagement.API.Domains.DTOs.Responses.Contract;
using ContractManagement.Domains.Interfaces.ContractTemplate;
using ContractManagement.Domains.Policies.ContractTemplate;
using ContractManagement.Domains.Services.Contract;
using ContractManagement.Domains.Services.ContractTemplate;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ContractManagement.Tests.Domains.Services.ContractTemplate;

public sealed class BilingualDocumentRenderingTests
{
    [Theory]
    [InlineData(0, "VND", "Zero Vietnamese dong only")]
    [InlineData(1001.5, "VND", "One thousand two Vietnamese dong only")]
    [InlineData(72187500, "VND", "Seventy-two million one hundred and eighty-seven thousand five hundred Vietnamese dong only")]
    [InlineData(1.01, "USD", "One US dollar and one cent")]
    [InlineData(2.995, "USD", "Three US dollars only")]
    [InlineData(0.25, "USD", "Zero US dollars and twenty-five cents")]
    public void EnglishMoney_UsesDecimalCurrencyPrecision(decimal value, string currency, string expected) =>
        Assert.Equal(expected, EnglishMoneyTextFormatter.Format(value, currency));

    [Theory]
    [InlineData(1.25, "Một USD và hai mươi lăm xu")]
    [InlineData(2.995, "Ba USD")]
    [InlineData(0.01, "Không USD và một xu")]
    public void VietnameseMoney_UsesSameUsdRoundingAsEnglish(decimal amount, string expected) =>
        Assert.Equal(expected, VietnameseMoneyTextFormatter.Format(amount, "USD"));

    [Theory]
    [InlineData("vi-VN")]
    [InlineData("ar-SA")]
    [InlineData("en-US")]
    public void EnglishDate_IndependentOfServerCulture(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            Assert.Equal("15 August 2026", DocumentEnglishPolicy.Date(new(2026, 8, 15)));
            Assert.Equal("One US dollar and twenty-five cents", EnglishMoneyTextFormatter.Format(1.25m, "USD"));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void UnsupportedCurrencyAndNegativeAmount_DoNotFallback()
    {
        Assert.Equal("EnglishCurrencyUnsupported", Assert.Throws<ContractTemplatePreviewException>(
            () => EnglishMoneyTextFormatter.Format(10, "EUR")).FailureCode);
        Assert.Throws<ArgumentOutOfRangeException>(() => EnglishMoneyTextFormatter.Format(-1, "VND"));
        Assert.NotEmpty(EnglishMoneyTextFormatter.Format(decimal.MaxValue, "VND"));
    }

    [Fact]
    public void Dependencies_OnlyUsedMarkersAndBlocksRequireMasterTranslations()
    {
        var context = new ContractPlaceholderResolveContext(new() { ContractDirection = 1, ContractNameEn = "Software agreement" },
            new(), new(), new() { LegalEntityNameEn = "Provider" }, null, null);
        var registry = new ContractPlaceholderSourceRegistry();
        Assert.Empty(ContractEnglishDependencies.FindMissing([], ContractLanguageMode.Bilingual, true, context, registry));
        var field = new TblContractTemplateField { IsSystem = true, PlaceholderKey = "CUSTOMER_ADDRESS_EN", SourceFieldKey = "system.CUSTOMER_ADDRESS_EN" };
        Assert.Empty(ContractEnglishDependencies.FindMissing([field], ContractLanguageMode.Vietnamese, false, context, registry));
        field.PlaceholderKey = "CONTRACT_APPENDICES";
        Assert.Empty(ContractEnglishDependencies.FindMissing([field], ContractLanguageMode.Bilingual, false, context, registry));
        Assert.Empty(ContractEnglishDependencies.FindMissing([field], ContractLanguageMode.Bilingual, true, context, registry));
        field.PlaceholderKey = "PROVIDER_BANK_NAME_EN";
        Assert.Equal("provider", Assert.Single(ContractEnglishDependencies.FindMissing([field], ContractLanguageMode.Bilingual, false, context, registry)).Area);
        Assert.Equal(string.Empty, registry.Format("counterparty.address-en", null, null, "Do not use this default"));
    }

    [Fact]
    public void TemplateRenderer_EnglishFirstAndCombinedOnlyOnce_PreservesItemGeometry()
    {
        var data = new ContractTemplateAuthoringPreviewData
        { ContractDirection = ContractManagement.API.Common.Enums.ContractDirection.Sale,
            Terms =
            [
                new(1, "Tiêu đề", "Heading", Rich("Nội dung Việt"), Rich("English body")),
                new(2, "Gộp", "Combined", Rich("ONE COMBINED BODY"), "") { BilingualContentMode = 2 }
            ],
            LegalBases = [new(1, Rich("Căn cứ Việt"), Rich("English basis"))],
            ItemTableColumnWidthsBps = ContractTableLayoutPolicy.DefaultItemColumnWidthsBps
        };
        var bytes = new ContractTemplatePreviewRenderer().RenderSample(Docx("{{CONTRACT_LEGAL_BASES}}", "{{CONTRACT_TERMS}}", "{{CONTRACT_ITEM_TABLE}}"),
            ContractLanguageMode.Bilingual, ContractPlaceholderCatalog.SystemDefinitions, new Dictionary<string, string>(), data);
        using var stream = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(stream, false);
        var body = doc.MainDocumentPart!.Document!.Body!;
        var text = body.InnerText;
        Assert.True(text.IndexOf("English basis", StringComparison.Ordinal) < text.IndexOf("Căn cứ Việt", StringComparison.Ordinal));
        Assert.Contains("Article 1. Heading / Điều 1. Tiêu đề", text);
        Assert.True(text.IndexOf("English body", StringComparison.Ordinal) < text.IndexOf("Nội dung Việt", StringComparison.Ordinal));
        Assert.Single(body.Descendants<W.Paragraph>(), x => x.InnerText == "ONE COMBINED BODY");
        var table = Assert.Single(body.Elements<W.Table>());
        Assert.Contains("DescriptionSản phẩm, dịch vụ", table.InnerText);
        Assert.Equal(2, table.Elements<W.TableRow>().First().Elements<W.TableCell>().ElementAt(2).Descendants<W.Break>().Count());
        var total = table.Elements<W.TableRow>().Last().Elements<W.TableCell>().ToArray();
        Assert.Equal("TOTALTỔNG CỘNG", total[0].InnerText);
        Assert.Equal(2, total[0].Descendants<W.Break>().Count());
        Assert.Equal(7, total[0].TableCellProperties!.GridSpan!.Val!.Value);
        Assert.Equal(W.JustificationValues.Right, total[0].GetFirstChild<W.Paragraph>()!.ParagraphProperties!.Justification!.Val!.Value);
    }

    [Theory]
    [InlineData(ContractLanguageMode.Bilingual)]
    [InlineData(ContractLanguageMode.Vietnamese)]
    public void ItemTable_LanguagesHaveOneBlankLine_WithoutChangingNumericCells(ContractLanguageMode languageMode)
    {
        var items = new[]
        {
            new ContractTemplateRenderItem(1, "Sản phẩm", "Phần mềm", 1, 100, "0%", "10%", 110)
                { TypeEn = "Product", DescriptionEn = "Software" },
            new ContractTemplateRenderItem(2, "Dịch vụ", "Triển khai", 2, 200, "0%", "10%", 440)
                { TypeEn = "Service", DescriptionEn = "Implementation" }
        };
        var data = new ContractTemplateRenderData(new Dictionary<string, string>(), items, [], new("", ""), new("", ""), "")
        { ContractDirection = ContractManagement.API.Common.Enums.ContractDirection.Sale,
            Definitions = ContractPlaceholderCatalog.SystemDefinitions,
            ItemTableColumnWidthsBps = ContractTableLayoutPolicy.DefaultItemColumnWidthsBps
        };
        var bytes = new ContractTemplatePreviewRenderer().Render(Docx("{{CONTRACT_ITEM_TABLE}}"), languageMode, data);
        using var stream = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(stream, false);
        var table = Assert.Single(doc.MainDocumentPart!.Document!.Body!.Elements<W.Table>());
        var rows = table.Elements<W.TableRow>().ToArray();
        var bilingual = languageMode == ContractLanguageMode.Bilingual;
        foreach (var (item, index) in items.Select((item, index) => (item, index)))
        {
            var cells = rows[index + 1].Elements<W.TableCell>().ToArray();
            Assert.Equal(bilingual ? $"{item.TypeEn}\n\n{item.Type}" : item.Type, TextWithBreaks(cells[1]));
            Assert.Equal(bilingual ? $"{item.DescriptionEn}\n\n{item.Description}" : item.Description, TextWithBreaks(cells[2]));
            foreach (var numericColumn in new[] { 0, 3, 4, 5, 6, 7 })
                Assert.Empty(cells[numericColumn].Descendants<W.Break>());
        }
        var totalLabel = rows.Last().Elements<W.TableCell>().First();
        Assert.Equal(bilingual ? "TOTAL\n\nTỔNG CỘNG" : "TỔNG CỘNG", TextWithBreaks(totalLabel));
        Assert.Equal(7, totalLabel.TableCellProperties!.GridSpan!.Val!.Value);
        Assert.Equal(W.JustificationValues.Right, totalLabel.GetFirstChild<W.Paragraph>()!.ParagraphProperties!.Justification!.Val!.Value);
    }

    private static string TextWithBreaks(W.TableCell cell) => string.Concat(cell.Descendants()
        .Select(element => element is W.Text text ? text.Text : element is W.Break ? "\n" : ""));

    [Fact]
    public void EnglishMarker_IsOptionalButCannotRenderVietnameseOrEmptyTranslation()
    {
        var data = new ContractTemplateRenderData(new Dictionary<string, string> { ["PROVIDER_BANK_NAME_EN"] = "" }, [], [], new("", ""), new("", ""), "")
        { ContractDirection = ContractManagement.API.Common.Enums.ContractDirection.Sale,  Definitions = ContractPlaceholderCatalog.SystemDefinitions };
        var renderer = new ContractTemplatePreviewRenderer();
        Assert.NotEmpty(renderer.Render(Docx("Static text"), ContractLanguageMode.Vietnamese, data));
        Assert.Equal("EnglishSourceMissing", Assert.Throws<ContractTemplatePreviewException>(() =>
            renderer.Render(Docx("{{PROVIDER_BANK_NAME_EN}}"), ContractLanguageMode.Vietnamese, data)).FailureCode);
    }

    [Theory]
    [InlineData("CUSTOMER_COMPANY_NAME_EN")]
    [InlineData("CUSTOMER_ADDRESS_EN")]
    [InlineData("CUSTOMER_BANK_NAME_EN")]
    [InlineData("CUSTOMER_CITY_EN")]
    [InlineData("CUSTOMER_COUNTRY_EN")]
    [InlineData("CUSTOMER_CONTACT_TITLE_EN")]
    public void CustomerEnglishMarker_MissingValueRendersBlank_NotVietnamese(string marker)
    {
        var data = new ContractTemplateRenderData(new Dictionary<string, string>
            { [marker.Replace("_EN", "")] = "KHÔNG DÙNG TIẾNG VIỆT THAY EN" }, [], [], new("", ""), new("", ""), "")
            { ContractDirection = ContractManagement.API.Common.Enums.ContractDirection.Sale,  Definitions = ContractPlaceholderCatalog.SystemDefinitions };
        var bytes = new ContractTemplatePreviewRenderer().Render(Docx($"EN[{{{{{marker}}}}}]"), ContractLanguageMode.Bilingual, data);
        using var doc = WordprocessingDocument.Open(new MemoryStream(bytes), false);
        Assert.Equal("EN[]", doc.MainDocumentPart!.Document!.Body!.InnerText);
    }

    [Theory]
    [InlineData("counterparty.company-en")]
    [InlineData("counterparty.address-en")]
    [InlineData("counterparty.bank-name-en")]
    [InlineData("counterparty.city-en")]
    [InlineData("counterparty.country-en")]
    [InlineData("counterparty.contact-title-en")]
    [InlineData("counterparty.representative-title-en")]
    public void CustomCustomerEnglishSource_IsOptionalForReadinessAndRendering(string source)
    {
        var field = new SoftwareSupplyPlaceholderDefinition("CUSTOM_EN", "Customer EN", true,
            TemplatePlaceholderDataKind.Scalar, TemplatePlaceholderMultiplicity.ZeroOrMore, "Customer")
            { IsSystem = false, SourceFieldKey = source, DefaultValue = "Do not use default text" };
        var context = new ContractPlaceholderResolveContext(new(), new(), new(), null, null, null);
        var missing = ContractEnglishDependencies.FindMissing([new()
            { PlaceholderKey = field.Key, SourceFieldKey = source, IsSystem = false }],
            ContractLanguageMode.Bilingual, false, context, new ContractPlaceholderSourceRegistry());
        Assert.Empty(missing);
        var data = new ContractTemplateRenderData(new Dictionary<string, string>(), [], [], new("", ""), new("", ""), "")
            { ContractDirection = ContractManagement.API.Common.Enums.ContractDirection.Sale,  Definitions = [field] };
        var bytes = new ContractTemplatePreviewRenderer().Render(Docx("EN[{{CUSTOM_EN}}]"), ContractLanguageMode.Bilingual, data);
        using var doc = WordprocessingDocument.Open(new MemoryStream(bytes), false);
        Assert.Equal("EN[]", doc.MainDocumentPart!.Document!.Body!.InnerText);
    }

    [Fact]
    public void CustomProviderEnglish_IsStillRequired_EvenWithCustomerLookingKey()
    {
        var field = new SoftwareSupplyPlaceholderDefinition("CUSTOMER_BANK_NAME_EN", "Provider bank", false,
            TemplatePlaceholderDataKind.Scalar, TemplatePlaceholderMultiplicity.ZeroOrMore, "Provider")
            { IsSystem = false, SourceFieldKey = "provider.bank-name-en" };
        var data = new ContractTemplateRenderData(new Dictionary<string, string>(), [], [], new("", ""), new("", ""), "")
            { ContractDirection = ContractManagement.API.Common.Enums.ContractDirection.Sale,  Definitions = [field] };
        Assert.Equal("EnglishSourceMissing", Assert.Throws<ContractTemplatePreviewException>(() =>
            new ContractTemplatePreviewRenderer().Render(Docx("{{CUSTOMER_BANK_NAME_EN}}"), ContractLanguageMode.Bilingual, data)).FailureCode);
    }

    [Theory]
    [InlineData("CUSTOMER_COMPANY_NAME_EN")]
    [InlineData("CUSTOMER_ADDRESS_EN")]
    [InlineData("CUSTOMER_REPRESENTATIVE_TITLE_EN")]
    public void AcceptanceCustomerEnglish_MissingValueRendersBlank(string marker)
    {
        var data = new ContractAcceptanceDocumentRenderData("AC-1", new(2026, 10, 18), "Hà Nội", ContractLanguageMode.Bilingual,
            [], [new() { PartyRole = AcceptancePartyRole.Customer, LegalName = "Tên khách hàng VI", Address = "Địa chỉ VI", RepresentativeTitle = "Chức danh VI" },
                new() { PartyRole = AcceptancePartyRole.Tenant, LegalName = "Bên cung cấp" }], []) { ContractDirection = ContractManagement.API.Common.Enums.ContractDirection.Sale };
        var bytes = new ContractAcceptanceDocumentRenderer().Render(Docx($"EN[{{{{{marker}}}}}]"), data);
        using var doc = WordprocessingDocument.Open(new MemoryStream(bytes), false);
        Assert.Equal("EN[]", doc.MainDocumentPart!.Document!.Body!.InnerText);
    }

    [Theory]
    [InlineData(ContractLanguageMode.Vietnamese,
        "ĐẠI DIỆN BÊN KHÁCH HÀNG", "ĐẠI DIỆN BÊN CUNG CẤP")]
    [InlineData(ContractLanguageMode.Bilingual,
        "CUSTOMER REPRESENTATIVE / ĐẠI DIỆN BÊN KHÁCH HÀNG",
        "PROVIDER REPRESENTATIVE / ĐẠI DIỆN BÊN CUNG CẤP")]
    public void SignatureMarkers_RenderOnlyHeadings_InBothRenderers(
        ContractLanguageMode language, string customerHeading, string providerHeading)
    {
        const string authoredText = "(Chữ ký) do người dùng soạn";
        var source = Docx("{{SIGNATURE_CUSTOMER}}", "{{SIGNATURE_PROVIDER}}", authoredText);
        var contractData = new ContractTemplateRenderData(new Dictionary<string, string>(), [], [],
            new("Tên bên cung cấp", "Người ký bên cung cấp"),
            new("Tên bên khách hàng", "Người ký bên khách hàng"), "Lời nhắc ký tự động")
        { ContractDirection = ContractManagement.API.Common.Enums.ContractDirection.Sale,  Definitions = ContractPlaceholderCatalog.SystemDefinitions };
        var acceptanceData = new ContractAcceptanceDocumentRenderData("AC-1", new(2026, 10, 18), "Hà Nội", language,
            [], [new() { PartyRole = AcceptancePartyRole.Customer, LegalName = "Tên bên khách hàng", RepresentativeName = "Người ký bên khách hàng" },
                new() { PartyRole = AcceptancePartyRole.Tenant, LegalName = "Tên bên cung cấp", RepresentativeName = "Người ký bên cung cấp" }], []) { ContractDirection = ContractManagement.API.Common.Enums.ContractDirection.Sale };

        foreach (var bytes in new[]
                 {
                     new ContractTemplatePreviewRenderer().Render(source, language, contractData),
                     new ContractAcceptanceDocumentRenderer().Render(source, acceptanceData)
                 })
        {
            using var doc = WordprocessingDocument.Open(new MemoryStream(bytes), false);
            var paragraphs = doc.MainDocumentPart!.Document!.Body!.Elements<W.Paragraph>().ToList();
            Assert.Equal(new[] { customerHeading, providerHeading, authoredText }, paragraphs.Select(p => p.InnerText));
            foreach (var paragraph in paragraphs.Take(2))
            {
                Assert.Equal(W.JustificationValues.Center, paragraph.ParagraphProperties!.Justification!.Val!.Value);
                Assert.All(paragraph.Descendants<W.Run>(), run =>
                {
                    var bold = Assert.IsType<W.Bold>(run.RunProperties?.Bold);
                    Assert.True(bold.Val?.Value ?? true);
                });
                Assert.Empty(paragraph.Descendants<W.Break>());
            }
        }
    }

    [Fact]
    public void AcceptanceRenderer_EnglishFirst_UsesDraftTranslationsAndMarkerAlignment()
    {
        var source = Docx("{{DOCUMENT_REFERENCES}}", "{{ACCEPTANCE_SECTIONS}}", "{{ACCEPTANCE_DATE_EN}}", "{{ACCEPTANCE_LOCATION_EN}}", "{{SIGNATURE_CUSTOMER}}");
        var data = new ContractAcceptanceDocumentRenderData("AC-1", new(2026, 10, 18), "Hà Nội", ContractLanguageMode.Bilingual,
            [new() { TextVi = "Căn cứ Việt", TextEn = "English signed reference" }],
            [new() { PartyRole = AcceptancePartyRole.Customer, LegalName = "Khách", RepresentativeName = "Nguyễn A" }, new() { PartyRole = AcceptancePartyRole.Tenant, LegalName = "Nhà cung cấp" }],
            [new() { TitleVi = "Kết quả", TitleEn = "Results", ContentVi = Rich("Nội dung Việt"), ContentEn = Rich("English result") },
             new() { TitleVi = "Gộp", TitleEn = "Combined", ContentVi = Rich("SINGLE ACCEPTANCE BODY"), BilingualContentMode = 2 }])
        { ContractDirection = ContractManagement.API.Common.Enums.ContractDirection.Sale,  LocationEn = "Hanoi" };
        var bytes = new ContractAcceptanceDocumentRenderer().Render(source, data);
        using var stream = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(stream, false);
        var body = doc.MainDocumentPart!.Document!.Body!;
        Assert.True(body.InnerText.IndexOf("English signed reference", StringComparison.Ordinal) < body.InnerText.IndexOf("Căn cứ Việt", StringComparison.Ordinal));
        Assert.True(body.InnerText.IndexOf("English result", StringComparison.Ordinal) < body.InnerText.IndexOf("Nội dung Việt", StringComparison.Ordinal));
        Assert.Contains("18 October 2026", body.InnerText);
        Assert.Contains("Hanoi", body.InnerText);
        Assert.Single(body.Descendants<W.Paragraph>(), p => p.InnerText == "SINGLE ACCEPTANCE BODY");
        var signature = Assert.Single(body.Descendants<W.Paragraph>(), p => p.InnerText.Contains("CUSTOMER REPRESENTATIVE"));
        Assert.Equal(W.JustificationValues.Center, signature.ParagraphProperties!.Justification!.Val!.Value);
        Assert.DoesNotContain("{{", body.InnerText);
    }

    [BilingualLibreOfficeFact]
    public async Task RealLibreOffice_BilingualContractAndAcceptance_ConvertWithoutDuplicatingTables()
    {
        var tableContent = ContractTermRichText.Prefix +
            """{"blocks":[{"type":"table","columnWidthsBps":[3000,7000],"rows":[{"cells":[{"paragraphs":[{"runs":[{"text":"Stage / Đợt","bold":true}]}]},{"paragraphs":[{"runs":[{"text":"Deliverable / Nội dung","bold":true}]}]}]},{"cells":[{"paragraphs":[{"runs":[{"text":"1"}]}]},{"paragraphs":[{"alignment":"left","runs":[{"text":"Software delivery / Bàn giao phần mềm"}]}]}]},{"cells":[{"colspan":2,"paragraphs":[{"alignment":"center","runs":[{"text":"One combined table / Một bảng song ngữ"}]}]}]}]}]}""";
        var authoring = new ContractTemplateAuthoringPreviewData
        { ContractDirection = ContractManagement.API.Common.Enums.ContractDirection.Sale,
            ItemTableColumnWidthsBps = [700, 1100, 2800, 700, 1300, 900, 700, 1800],
            LegalBases = [new(1, Rich("Căn cứ nhu cầu và khả năng của các Bên."), Rich("Based on the needs and capabilities of the Parties."))],
            Terms = [new(1, "Bàn giao", "Delivery", tableContent, "") { BilingualContentMode = 2 }],
            Appendices = [new(1, "PL-01", "Phạm vi triển khai", "Implementation scope",
                "Căn cứ Hợp đồng số SAMPLE giữa Công ty DTC Mẫu và Khách hàng Mẫu.",
                "Pursuant to Contract No. SAMPLE between DTC Sample Company and Sample Customer.",
                [new(1, "Bổ sung phạm vi", "Additional scope", Rich("Nội dung bổ sung bằng tiếng Việt."), Rich("Additional English scope."))])]
        };
        var contract = new ContractTemplatePreviewRenderer().RenderSample(Docx(
            "SOFTWARE SUPPLY AGREEMENT / HỢP ĐỒNG CUNG CẤP PHẦN MỀM",
            "{{PROVIDER_COMPANY_NAME_EN}} / {{PROVIDER_COMPANY_NAME}}", "{{CONTRACT_LEGAL_BASES}}",
            "{{CONTRACT_ITEM_TABLE}}", "{{CONTRACT_TERMS}}", "{{SIGNATURE_PROVIDER}}", "{{CONTRACT_APPENDICES}}"),
            ContractLanguageMode.Bilingual, ContractPlaceholderCatalog.SystemDefinitions, new Dictionary<string, string>(), authoring);
        var acceptance = new ContractAcceptanceDocumentRenderer().Render(Docx(
            "SOFTWARE ACCEPTANCE RECORD / BIÊN BẢN NGHIỆM THU PHẦN MỀM",
            "{{DOCUMENT_REFERENCES}}", "{{ACCEPTANCE_DATE_EN}} / {{ACCEPTANCE_DATE}}",
            "{{ACCEPTANCE_LOCATION_EN}} / {{ACCEPTANCE_LOCATION}}", "{{ACCEPTANCE_SECTIONS}}",
            "{{SIGNATURE_CUSTOMER}}", "{{SIGNATURE_PROVIDER}}"), new("BBNT-SAMPLE", new(2026, 10, 18), "Hà Nội",
            ContractLanguageMode.Bilingual,
            [new() { TextEn = "Pursuant to Contract No. SAMPLE signed on 01 October 2026 between DTC Sample and Sample Customer.", TextVi = "Căn cứ Hợp đồng số SAMPLE ký ngày 01/10/2026 giữa Công ty DTC Mẫu và Khách hàng Mẫu." }],
            [new() { PartyRole = AcceptancePartyRole.Customer, LegalName = "Khách hàng Mẫu", RepresentativeName = "Nguyễn Khách Hàng" },
             new() { PartyRole = AcceptancePartyRole.Tenant, LegalName = "Công ty DTC Mẫu", RepresentativeName = "Trần Nhà Cung Cấp" }],
            [new() { TitleVi = "Kết quả thực hiện", TitleEn = "Delivery results", ContentVi = tableContent, BilingualContentMode = 2 }])
            { ContractDirection = ContractManagement.API.Common.Enums.ContractDirection.Sale, LocationEn = "Hanoi" });
        var converter = new LibreOfficeContractTemplatePdfRenderer(Microsoft.Extensions.Options.Options.Create(new TemplatePdfRenderingOptions
        {
            ExecutablePath = Environment.GetEnvironmentVariable("PHASE12_LIBREOFFICE_PATH")!, TimeoutSeconds = 60, MaxOutputBytes = 25 * 1024 * 1024
        }));
        foreach (var (name, docx) in new[] { ("test-contract", contract), ("test-acceptance", acceptance) })
        {
            var pdf = await converter.ConvertPreviewToPdfAsync(docx);
            Assert.True(pdf.AsSpan(0, 5).SequenceEqual("%PDF-"u8));
            if (Environment.GetEnvironmentVariable("BILINGUAL_ARTIFACT_DIRECTORY") is { Length: > 0 } directory)
            {
                Directory.CreateDirectory(directory);
                await System.IO.File.WriteAllBytesAsync(Path.Combine(directory, name + ".docx"), docx);
                await System.IO.File.WriteAllBytesAsync(Path.Combine(directory, name + ".pdf"), pdf);
            }
        }
    }

    private sealed class BilingualLibreOfficeFactAttribute : FactAttribute
    {
        public BilingualLibreOfficeFactAttribute()
        {
            if (!System.IO.File.Exists(Environment.GetEnvironmentVariable("PHASE12_LIBREOFFICE_PATH")))
                Skip = "Set PHASE12_LIBREOFFICE_PATH to verify real bilingual PDF conversion.";
        }
    }

    private static string Rich(string text) => ContractTermRichText.Prefix + System.Text.Json.JsonSerializer.Serialize(
        new { blocks = new[] { new { type = "paragraph", runs = new[] { new { text } } } } });

    private static byte[] Docx(params string[] paragraphs)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new W.Document(new W.Body(paragraphs.Select(text => new W.Paragraph(
                new W.ParagraphProperties(new W.Justification { Val = text.Contains("SIGNATURE", StringComparison.Ordinal) ? W.JustificationValues.Center : W.JustificationValues.Left }),
                new W.Run(new W.Text(text))))));
            main.Document.Body!.Append(new W.SectionProperties());
            main.Document.Save();
        }
        return stream.ToArray();
    }
}
