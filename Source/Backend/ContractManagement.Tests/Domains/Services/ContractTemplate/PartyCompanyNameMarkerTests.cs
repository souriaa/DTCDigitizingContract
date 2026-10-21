using ContractManagement.API.Common.Enums;
using ContractManagement.API.Domains.Models.Contract;
using ContractManagement.Common.Enums;
using ContractManagement.Domains.Policies.ContractTemplate;
using ContractManagement.Domains.Services.ContractTemplate;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.AspNetCore.Http;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ContractManagement.Tests.Domains.Services.ContractTemplate;

public sealed class PartyCompanyNameMarkerTests
{
    [Theory]
    [InlineData(ContractDirection.Sale)]
    [InlineData(ContractDirection.Purchase)]
    public void NamesAndRepresentativePhone_FollowRoles_WithoutUsingCompanyPhone(ContractDirection direction)
    {
        var tenant = new TblTenantLegalProfile { LegalEntityName = "Tenant", LegalEntityNameEn = "Tenant EN", PhoneNumber = "024-company" };
        var external = new TblCustomer { CustomerCompany = "External", CustomerCompanyEn = "External EN",
            CustomerPhone = "028-company", CustomerMobile = "098-mobile", CustomerContactPersonPhone = "090-contact" };
        var values = DocumentPartyMarkers.Values(direction, DocumentParty.From(tenant), DocumentParty.From(external));
        Assert.Equal(direction == ContractDirection.Sale ? "Tenant" : "External", values["PROVIDER_COMPANY_NAME"]);
        Assert.Equal(direction == ContractDirection.Sale ? "External EN" : "Tenant EN", values["CUSTOMER_COMPANY_NAME_EN"]);
        Assert.Equal(direction == ContractDirection.Sale ? "090-contact" : "", values["CUSTOMER_REPRESENTATIVE_PHONE"]);
        var context = new ContractPlaceholderResolveContext(new() { ContractDirection = (byte)direction }, new(), external, tenant, null, null);
        var registry = new ContractPlaceholderSourceRegistry();
        Assert.Equal(values["CUSTOMER_REPRESENTATIVE_PHONE"], registry.Resolve("customer.representative-phone", context));
        Assert.Equal(values["CUSTOMER_COMPANY_NAME"], registry.Resolve("customer.legal-name", context));
        external.CustomerContactPersonPhone = null;
        values = DocumentPartyMarkers.Values(direction, DocumentParty.From(tenant), DocumentParty.From(external));
        Assert.Equal("", values["CUSTOMER_REPRESENTATIVE_PHONE"]);
    }

    [Fact]
    public void FrozenContactPhone_ComesFromSignedSnapshot_NotOrganizationPhone()
    {
        var source = new CustomerLegalSnapshot(1, "External", null, "Address", "Legal representative", "Director",
            "028-company", null, null, null) { ContactName = "Contact", ContactPhone = "090-frozen" };
        Assert.Equal("090-frozen", DocumentParty.From(source).Get("REPRESENTATIVE_PHONE"));
        var relational = FrozenContractPartySources.Counterparty(new TblContractVersionPartySnapshot {
            SourceCustomerId = 1, LegalName = "External", ContactPhone = "090-relational", PhoneNumber = "028-company" });
        Assert.Equal("090-relational", DocumentParty.From(relational).Get("REPRESENTATIVE_PHONE"));
    }

    [Theory]
    [InlineData(TemplateDocumentType.SoftwareSupplyContract)]
    [InlineData(TemplateDocumentType.AcceptanceRecord)]
    [InlineData(TemplateDocumentType.HandoverRecord)]
    public async Task NewMarkers_ValidateAndRenderPreviewAcrossDocumentTypes(TemplateDocumentType type)
    {
        var definitions = type == TemplateDocumentType.SoftwareSupplyContract ? SoftwareSupplyPlaceholderCatalog.All : SupportingDocumentCatalog.Get(type);
        var bytes = Docx("{{PROVIDER_COMPANY_NAME}}", "{{CUSTOMER_COMPANY_NAME}}", "{{PROVIDER_COMPANY_NAME_EN}}",
            "{{CUSTOMER_COMPANY_NAME_EN}}", "{{CUSTOMER_REPRESENTATIVE_PHONE}}");
        var validation = await new ContractTemplateDocumentValidator().ValidateAsync(File(bytes), type);
        Assert.True(validation.IsCatalogValid, validation.ValidationMessage);
        var rendered = new ContractTemplatePreviewRenderer().RenderSample(bytes, ContractLanguageMode.Bilingual,
            definitions, new Dictionary<string, string>(), new() { ContractDirection = ContractDirection.Sale });
        using var document = WordprocessingDocument.Open(new MemoryStream(rendered), false);
        var text = document.MainDocumentPart!.Document!.InnerText;
        Assert.DoesNotContain("{{", text);
        var samples = SoftwareSupplyPreviewDatasetV1.GetScalarValues(ContractLanguageMode.Bilingual);
        Assert.Contains(samples["PROVIDER_COMPANY_NAME"], text);
        Assert.Contains(samples["CUSTOMER_COMPANY_NAME_EN"], text);
        Assert.Contains("+84 90 123 0001", text);
    }

    [Theory]
    [InlineData("PROVIDER_LEGAL_NAME")]
    [InlineData("PROVIDER_LEGAL_NAME_EN")]
    [InlineData("CUSTOMER_LEGAL_NAME")]
    [InlineData("CUSTOMER_LEGAL_NAME_EN")]
    public async Task RenamedMarkers_AreNotAcceptedAsAliases(string key)
    {
        foreach (var type in new[] { TemplateDocumentType.SoftwareSupplyContract, TemplateDocumentType.AcceptanceRecord, TemplateDocumentType.HandoverRecord })
        {
            var validation = await new ContractTemplateDocumentValidator().ValidateAsync(File(Docx("{{" + key + "}}")), type);
            Assert.False(validation.IsCatalogValid);
            Assert.Contains("UnknownPlaceholder:" + key, validation.ValidationMessage);
        }
    }

    private static IFormFile File(byte[] bytes) => new FormFile(new MemoryStream(bytes), 0, bytes.Length, "File", "markers.docx");
    private static byte[] Docx(params string[] markers)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var part = document.AddMainDocumentPart();
            part.Document = new W.Document(new W.Body(markers.Select(x => new W.Paragraph(new W.Run(new W.Text(x))))));
            part.Document.Save();
        }
        return stream.ToArray();
    }
}
