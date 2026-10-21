using ContractManagement.API.Common.Enums;
using ContractManagement.Domains.Policies.ContractTemplate;
using ContractManagement.Domains.Services.ContractTemplate;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.Extensions.Options;
using W = DocumentFormat.OpenXml.Wordprocessing;
using Xunit;
using static ContractManagement.Tests.ContractRichTextTestData;

namespace ContractManagement.Tests.Integration;

public sealed class HandoverLibreOfficeTests
{
    [HandoverLibreTheory]
    [InlineData(ContractDirection.Sale, ContractLanguageMode.Vietnamese)]
    [InlineData(ContractDirection.Purchase, ContractLanguageMode.Vietnamese)]
    [InlineData(ContractDirection.Sale, ContractLanguageMode.Bilingual)]
    [InlineData(ContractDirection.Purchase, ContractLanguageMode.Bilingual)]
    [Trait("Category","Phase12LibreOffice")]
    public async Task RealConverter_ConvertsRenderedHandoverWithTableReferencesAndSignatures(
        ContractDirection direction, ContractLanguageMode language)
    {
        using var source = new MemoryStream();
        using (var doc=WordprocessingDocument.Create(source,WordprocessingDocumentType.Document))
        {
            var main=doc.AddMainDocumentPart();var body=new W.Body();
            foreach(var key in new[]{"HANDOVER_CODE","DOCUMENT_REFERENCES","HANDOVER_ITEM_TABLE","HANDOVER_SECTIONS","SIGNATURE_PROVIDER","SIGNATURE_CUSTOMER"})
                body.Append(new W.Paragraph(new W.ParagraphProperties(new W.Justification{Val=W.JustificationValues.Center}),
                    new W.Run(new W.RunProperties(new W.RunFonts{Ascii="Times New Roman",HighAnsi="Times New Roman"},new W.FontSize{Val="24"}),new W.Text("{{"+key+"}}"))));
            main.Document=new W.Document(body);main.Document.Save();
        }
        var docx=new ContractTemplatePreviewRenderer().RenderSample(source.ToArray(),language,
            HandoverRecordPlaceholderCatalog.All,new Dictionary<string,string>(),new ContractTemplateAuthoringPreviewData{
                ContractDirection=direction,
                Terms=[new(1,"Kết quả bàn giao","Handover results",RichText("Nội dung kết quả bàn giao"),language==ContractLanguageMode.Bilingual?RichText("Delivered scope"):"")]
            });
        using(var doc=WordprocessingDocument.Open(new MemoryStream(docx),false))
        {
            Assert.DoesNotContain("{{",doc.MainDocumentPart!.Document!.InnerText);
            Assert.Single(doc.MainDocumentPart.Document.Descendants<W.Table>());
            Assert.Contains("ĐẠI DIỆN BÊN CUNG CẤP",doc.MainDocumentPart.Document.InnerText);
        }
        var converter=new LibreOfficeContractTemplatePdfRenderer(Options.Create(new TemplatePdfRenderingOptions{
            ExecutablePath=Environment.GetEnvironmentVariable("PHASE12_LIBREOFFICE_PATH")!,TimeoutSeconds=60,MaxOutputBytes=25*1024*1024}));
        var pdf=await converter.ConvertPreviewToPdfAsync(docx);
        Assert.True(pdf.Length>100);Assert.True(pdf.AsSpan(0,5).SequenceEqual("%PDF-"u8));
    }

    private sealed class HandoverLibreTheoryAttribute:TheoryAttribute
    {
        public HandoverLibreTheoryAttribute()
        {
            var path=Environment.GetEnvironmentVariable("PHASE12_LIBREOFFICE_PATH");
            if(string.IsNullOrWhiteSpace(path)||!File.Exists(path))Skip="Set PHASE12_LIBREOFFICE_PATH to allow the real converter test.";
        }
    }
}
