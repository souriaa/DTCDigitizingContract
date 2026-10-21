using ContractManagement.API.Common.Enums;
using ContractManagement.API.Domains.DTOs.Responses.Contract;
using ContractManagement.Domains.Policies.ContractTemplate;
using ContractManagement.Domains.Services.Contract;
using ContractManagement.Domains.Services.ContractTemplate;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Options;

namespace ContractManagement.Tests.Integration;

using static ContractManagement.Tests.ContractRichTextTestData;

public sealed class Phase12LibreOfficeReleaseGateTests
{
    private const string ExecutableVariable =
        "PHASE12_LIBREOFFICE_PATH";

    [Phase12LibreOfficeFact]
    [Trait("Category", "Phase12LibreOffice")]
    public async Task RealLibreOffice_ConvertsGeneratedDocxToValidPdf()
    {
        var executablePath = Environment.GetEnvironmentVariable(
            ExecutableVariable)!;
        Assert.True(File.Exists(executablePath));
        var renderer = new LibreOfficeContractTemplatePdfRenderer(
            Options.Create(new TemplatePdfRenderingOptions
            {
                ExecutablePath = executablePath,
                TimeoutSeconds = 60,
                MaxOutputBytes = 25 * 1024 * 1024
            }));

        var pdf = await renderer.ConvertPreviewToPdfAsync(CreateDocument());

        Assert.True(pdf.Length > 5);
        Assert.True(pdf.AsSpan(0, 5).SequenceEqual("%PDF-"u8));
    }

    [Phase12LibreOfficeFact]
    [Trait("Category", "Phase12LibreOffice")]
    public async Task RealLibreOffice_ConvertsRenderedAppendixPackageToValidPdf()
    {
        var executablePath = Environment.GetEnvironmentVariable(
            ExecutableVariable)!;
        var renderer = new ContractTemplatePreviewRenderer();
        var appendixDefinition = ContractPlaceholderCatalog.SystemDefinitions
            .Single(definition => definition.Key == "CONTRACT_APPENDICES");
        var renderData = new ContractTemplateRenderData(
            new Dictionary<string, string>(StringComparer.Ordinal),
            [],
            [],
            new ContractTemplateRenderSignature("BÊN A", "Đại diện A"),
            new ContractTemplateRenderSignature("BÊN B", "Đại diện B"),
            string.Empty)
        { ContractDirection = ContractManagement.API.Common.Enums.ContractDirection.Sale,
            Definitions = [appendixDefinition],
            Appendices =
            [
                new ContractTemplateRenderAppendix(
                    1,
                    "PL-01",
                    "Phạm vi triển khai",
                    null,
                    "Căn cứ Hợp đồng số HD-01 giữa Bên A và Bên B về việc Cung cấp phần mềm.",
                    null,
                    [new ContractTemplateRenderAppendixTerm(
                        1,
                        "Nội dung phụ lục",
                        null,
                        null,
                        null)])
            ]
        };
        var docx = renderer.Render(
            CreateAppendixSourceDocument(),
            ContractLanguageMode.Vietnamese,
            renderData);
        using (var document = WordprocessingDocument.Open(
                   new MemoryStream(docx), false))
        {
            Assert.Contains(
                "PHỤ LỤC PL-01: Phạm vi triển khai",
                document.MainDocumentPart!.Document!.InnerText);
        }

        var pdfRenderer = new LibreOfficeContractTemplatePdfRenderer(
            Options.Create(new TemplatePdfRenderingOptions
            {
                ExecutablePath = executablePath,
                TimeoutSeconds = 60,
                MaxOutputBytes = 25 * 1024 * 1024
            }));

        var pdf = await pdfRenderer.ConvertPreviewToPdfAsync(docx);

        Assert.True(pdf.Length > 5);
        Assert.True(pdf.AsSpan(0, 5).SequenceEqual("%PDF-"u8));
    }

    [Phase12LibreOfficeFact]
    [Trait("Category", "Phase12LibreOffice")]
    public async Task RealLibreOffice_ConvertsAcceptanceRecordWithGeneratedReferences()
    {
        var renderer = new ContractAcceptanceDocumentRenderer();
        var docx = renderer.Render(
            CreateAcceptanceSourceDocument(),
            new ContractAcceptanceDocumentRenderData(
                "BBNT-01",
                new DateTime(2026, 9, 19),
                "Hà Nội",
                ContractLanguageMode.Vietnamese,
                [
                    new ContractAcceptanceReferenceResponse
                    {
                        ReferenceType = AcceptanceReferenceType.Contract,
                        DisplayOrder = 0,
                        TextVi = "Căn cứ Hợp đồng số HD-01 ký ngày 12/09/2026 giữa Công ty DTC và Khách hàng A về việc Cung cấp phần mềm."
                    },
                    new ContractAcceptanceReferenceResponse
                    {
                        ReferenceType = AcceptanceReferenceType.Appendix,
                        AppendixId = 1,
                        DisplayOrder = 1,
                        TextVi = "Căn cứ Phụ lục PL-01 - Nâng cấp phần mềm thuộc Hợp đồng số HD-01."
                    }
                ],
                [
                    new ContractAcceptancePartyResponse
                    {
                        PartyRole = AcceptancePartyRole.Customer,
                        LegalName = "Khách hàng A",
                        RepresentativeName = "Đại diện A"
                    },
                    new ContractAcceptancePartyResponse
                    {
                        PartyRole = AcceptancePartyRole.Tenant,
                        LegalName = "Công ty DTC",
                        RepresentativeName = "Đại diện B"
                    }
                ],
                [
                    new ContractAcceptanceSectionResponse
                    {
                        SectionCode = "RESULT",
                        TitleVi = "I. KẾT QUẢ THỰC HIỆN",
                        ContentVi = RichText("Hai bên xác nhận đã hoàn thành."),
                        DisplayOrder = 1
                    }
                ]) { ContractDirection = ContractManagement.API.Common.Enums.ContractDirection.Sale });

        using (var document = WordprocessingDocument.Open(
                   new MemoryStream(docx), false))
        {
            var text = document.MainDocumentPart!.Document!.InnerText;
            Assert.Contains("ký ngày 12/09/2026", text);
            Assert.Contains("Căn cứ Phụ lục PL-01", text);
            Assert.DoesNotContain("Phụ lục PL-01 ký ngày", text);
            Assert.Contains("Hai bên xác nhận đã hoàn thành.", text);
        }

        var pdfRenderer = new LibreOfficeContractTemplatePdfRenderer(
            Options.Create(new TemplatePdfRenderingOptions
            {
                ExecutablePath = Environment.GetEnvironmentVariable(
                    ExecutableVariable)!,
                TimeoutSeconds = 60,
                MaxOutputBytes = 25 * 1024 * 1024
            }));
        var pdf = await pdfRenderer.ConvertPreviewToPdfAsync(docx);
        Assert.True(pdf.Length > 5);
        Assert.True(pdf.AsSpan(0, 5).SequenceEqual("%PDF-"u8));
    }

    private static byte[] CreateDocument()
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(
                   stream,
                   DocumentFormat.OpenXml.WordprocessingDocumentType.Document,
                   autoSave: true))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new Document(
                new Body(
                    new Paragraph(
                        new Run(
                            new Text("Phase 12 LibreOffice release gate")))));
            main.Document.Save();
        }

        return stream.ToArray();
    }

    private static byte[] CreateAppendixSourceDocument()
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(
                   stream,
                   DocumentFormat.OpenXml.WordprocessingDocumentType.Document,
                   autoSave: true))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new Document(
                new Body(
                    new Paragraph(new Run(new Text("HỢP ĐỒNG GỐC"))),
                    new Paragraph(new Run(new Text(
                        "{{CONTRACT_APPENDICES}}")))));
            main.Document.Save();
        }

        return stream.ToArray();
    }

    private static byte[] CreateAcceptanceSourceDocument()
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(
                   stream,
                   DocumentFormat.OpenXml.WordprocessingDocumentType.Document,
                   autoSave: true))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new Document(
                new Body(
                    new Paragraph(new Run(new Text(
                        "BIÊN BẢN NGHIỆM THU {{ACCEPTANCE_CODE}}"))),
                    new Paragraph(new Run(new Text(
                        "{{DOCUMENT_REFERENCES}}"))),
                    new Paragraph(new Run(new Text(
                        "{{ACCEPTANCE_SECTIONS}}")))));
            main.Document.Save();
        }
        return stream.ToArray();
    }

    private sealed class Phase12LibreOfficeFactAttribute : FactAttribute
    {
        public Phase12LibreOfficeFactAttribute()
        {
            var executable = Environment.GetEnvironmentVariable(
                ExecutableVariable);
            if (string.IsNullOrWhiteSpace(executable)
                || !File.Exists(executable))
            {
                Skip = $"Set {ExecutableVariable} to a LibreOffice executable.";
            }
        }
    }
}
