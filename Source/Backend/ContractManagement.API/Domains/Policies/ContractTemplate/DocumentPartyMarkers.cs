using ContractManagement.API.Common.Enums;
using ContractManagement.API.Domains.DTOs.Responses.Contract;
using ContractManagement.API.Domains.Models.Contract;
using ContractManagement.Domains.Policies.Contract;
using ContractManagement.Infrastructure.Persistence.Application.Models;

namespace ContractManagement.Domains.Policies.ContractTemplate;

/// <summary>Normalized subject data. Missing fields stay empty; never translate or read a master from a renderer.</summary>
public sealed record DocumentParty(IReadOnlyDictionary<string, string> Values)
{
    public string Get(string key) => Values.GetValueOrDefault(key) ?? string.Empty;
    private static DocumentParty Create(params (string Key, string? Value)[] values) =>
        new(values.ToDictionary(x => x.Key, x => x.Value?.Trim() ?? string.Empty, StringComparer.Ordinal));

    public static DocumentParty From(TblTenantLegalProfile? x) => x is null ? Create() : Create(
        ("COMPANY_NAME", x.LegalEntityName), ("COMPANY_NAME_EN", x.LegalEntityNameEn), ("ADDRESS", x.Address), ("ADDRESS_EN", x.AddressEn),
        ("TAX_CODE", x.TaxCode), ("REPRESENTATIVE_NAME", x.RepresentativeName), ("REPRESENTATIVE_TITLE", x.RepresentativeTitle),
        ("REPRESENTATIVE_TITLE_EN", x.RepresentativeTitleEn), ("PHONE", x.PhoneNumber), ("FAX", x.FaxNumber),
        ("BANK_ACCOUNT_NUMBER", x.BankAccountNumber), ("BANK_NAME", x.BankName), ("BANK_NAME_EN", x.BankNameEn));
    public static DocumentParty From(TblCustomer x) => Create(
        ("COMPANY_NAME", x.CustomerCompany), ("COMPANY_NAME_EN", x.CustomerCompanyEn), ("ADDRESS", x.CustomerAddress), ("ADDRESS_EN", x.CustomerAddressEn),
        ("TAX_CODE", x.CustomerTaxCode), ("REPRESENTATIVE_NAME", x.CustomerContactPersonName), ("REPRESENTATIVE_TITLE", x.CustomerContactPersonTitle),
        ("REPRESENTATIVE_TITLE_EN", x.CustomerContactPersonTitleEn), ("PHONE", x.CustomerPhone), ("FAX", x.CustomerFaxNumber),
        ("BANK_ACCOUNT_NUMBER", x.CustomerBankAccountNumber), ("BANK_NAME", x.CustomerBankName), ("BANK_NAME_EN", x.CustomerBankNameEn),
        ("CODE", x.CustomerCode), ("NAME", x.CustomerFullName), ("EMAIL", x.CustomerEmail), ("WEBSITE", x.CustomerWebsite),
        ("CITY", x.CustomerCity), ("CITY_EN", x.CustomerCityEn), ("COUNTRY", x.CustomerCountry), ("COUNTRY_EN", x.CustomerCountryEn),
        ("CONTACT_TITLE_EN", x.CustomerContactPersonTitleEn), ("REPRESENTATIVE_PHONE", x.CustomerContactPersonPhone));
    public static DocumentParty From(TenantLegalSnapshot x) => Create(
        ("COMPANY_NAME", x.LegalEntityName), ("COMPANY_NAME_EN", x.LegalNameEn), ("ADDRESS", x.Address), ("ADDRESS_EN", x.AddressEn),
        ("TAX_CODE", x.TaxCode), ("REPRESENTATIVE_NAME", x.RepresentativeName), ("REPRESENTATIVE_TITLE", x.RepresentativeTitle),
        ("REPRESENTATIVE_TITLE_EN", x.RepresentativeTitleEn), ("PHONE", x.PhoneNumber), ("FAX", x.FaxNumber),
        ("BANK_ACCOUNT_NUMBER", x.BankAccountNumber), ("BANK_NAME", x.BankName), ("BANK_NAME_EN", x.BankNameEn),
        ("CITY_EN", x.CityEn), ("COUNTRY_EN", x.CountryEn), ("CONTACT_TITLE_EN", x.ContactTitleEn));
    public static DocumentParty From(CustomerLegalSnapshot x) => Create(
        ("CODE", x.SubjectCode), ("NAME", x.FullName), ("EMAIL", x.Email), ("WEBSITE", x.Website), ("CITY", x.City), ("COUNTRY", x.Country),
        ("COMPANY_NAME", x.LegalName), ("COMPANY_NAME_EN", x.LegalNameEn), ("ADDRESS", x.Address), ("ADDRESS_EN", x.AddressEn),
        ("TAX_CODE", x.TaxCode), ("REPRESENTATIVE_NAME", x.ContactName), ("REPRESENTATIVE_TITLE", x.ContactTitle),
        ("REPRESENTATIVE_TITLE_EN", x.ContactTitleEn), ("PHONE", x.PhoneNumber), ("FAX", x.FaxNumber),
        ("BANK_ACCOUNT_NUMBER", x.BankAccountNumber), ("BANK_NAME", x.BankName), ("BANK_NAME_EN", x.BankNameEn),
        ("CITY_EN", x.CityEn), ("COUNTRY_EN", x.CountryEn), ("CONTACT_TITLE_EN", x.ContactTitleEn), ("REPRESENTATIVE_PHONE", x.ContactPhone));
    public static DocumentParty From(ContractAcceptancePartyResponse x) => Create(
        ("COMPANY_NAME", x.LegalName), ("COMPANY_NAME_EN", x.LegalNameEn), ("ADDRESS", x.Address), ("ADDRESS_EN", x.AddressEn),
        ("TAX_CODE", x.TaxCode), ("REPRESENTATIVE_NAME", x.RepresentativeName), ("REPRESENTATIVE_TITLE", x.RepresentativeTitle),
        ("REPRESENTATIVE_TITLE_EN", x.RepresentativeTitleEn), ("PHONE", x.Phone), ("FAX", x.Fax));
}

public static class DocumentPartyMarkers
{
    public static readonly IReadOnlyList<(string Suffix, string Label)> Fields =
    [
        ("COMPANY_NAME", "Tên công ty / pháp nhân"),
        ("COMPANY_NAME_EN", "Tên công ty / pháp nhân tiếng Anh"),
        ("ADDRESS", "Địa chỉ"),
        ("ADDRESS_EN", "Địa chỉ tiếng Anh"),
        ("TAX_CODE", "Mã số thuế"),
        ("REPRESENTATIVE_NAME", "Người đại diện tham gia"),
        ("REPRESENTATIVE_TITLE", "Chức vụ"),
        ("REPRESENTATIVE_TITLE_EN", "Chức vụ tiếng Anh"),
        ("PHONE", "Điện thoại"),
        ("FAX", "Fax"),
        ("BANK_ACCOUNT_NUMBER", "Số tài khoản ngân hàng"),
        ("BANK_NAME", "Tên ngân hàng"),
        ("BANK_NAME_EN", "Tên ngân hàng tiếng Anh"),
        ("CODE", "Mã chủ thể"),
        ("NAME", "Tên liên hệ"),
        ("EMAIL", "Email"),
        ("WEBSITE", "Website"),
        ("CITY", "Tỉnh/thành"),
        ("CITY_EN", "Tỉnh/thành tiếng Anh"),
        ("COUNTRY", "Quốc gia"),
        ("COUNTRY_EN", "Quốc gia tiếng Anh"),
        ("CONTACT_TITLE_EN", "Chức danh người liên lạc tiếng Anh"),
    ];
    public static IReadOnlyList<SoftwareSupplyPlaceholderDefinition> Definitions { get; } =
        new[] { "PROVIDER", "CUSTOMER" }.SelectMany(role => Fields.Select(field =>
            Field(role, field.Suffix, field.Label)))
        .Append(Field("CUSTOMER", "REPRESENTATIVE_PHONE", "Điện thoại người đại diện tham gia / người liên lạc")).ToArray();

    private static SoftwareSupplyPlaceholderDefinition Field(string role, string suffix, string label) =>
        new(role + "_" + suffix, (role == "PROVIDER" ? "Bên cung cấp — " : "Bên mua — ") + label, false,
            TemplatePlaceholderDataKind.Scalar, TemplatePlaceholderMultiplicity.ZeroOrMore,
            role + "." + suffix) { SourceFieldKey = "system." + role + "_" + suffix };

    public static Dictionary<string, string> Values(ContractDirection direction, DocumentParty tenant, DocumentParty counterparty)
    {
        var parties = ContractPartyRoleResolver.Resolve(direction, tenant, counterparty);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in Fields)
        {
            result["PROVIDER_" + field.Suffix] = parties.Provider.Value.Get(field.Suffix);
            result["CUSTOMER_" + field.Suffix] = parties.Customer.Value.Get(field.Suffix);
        }
        result["CUSTOMER_REPRESENTATIVE_PHONE"] = parties.Customer.Value.Get("REPRESENTATIVE_PHONE");
        return result;
    }

    public static string SourceSuffix(string suffix) => suffix.ToLowerInvariant().Replace("_", "-")
        .Replace("bank-account-number", "bank-account")
        // Marker wording changes do not rename the stable domain source keys.
        .Replace("company-name", "legal-name");
}

