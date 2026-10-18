using System.Globalization;
using ContractManagement.Domains.Interfaces.ContractTemplate;
using ContractManagement.API.Common.Enums;

namespace ContractManagement.Domains.Policies.ContractTemplate;

/// <summary>Language order and strict English dependencies shared by both document renderers.</summary>
public static class DocumentEnglishPolicy
{
    private static readonly HashSet<string> OptionalCustomerSources = new(StringComparer.Ordinal)
    {
        "customer.company-en", "customer.address-en", "customer.bank-name-en", "customer.city-en",
        "customer.country-en", "customer.contact-title-en", "customer.representative-title-en"
    };

    private static readonly HashSet<string> OptionalCustomerMarkers = new(StringComparer.Ordinal)
    {
        "CUSTOMER_COMPANY_EN", "CUSTOMER_ADDRESS_EN", "CUSTOMER_BANK_NAME_EN", "CUSTOMER_CITY_EN",
        "CUSTOMER_COUNTRY_EN", "CUSTOMER_CONTACT_TITLE_EN", "CUSTOMER_LEGAL_NAME_EN", "CUSTOMER_REPRESENTATIVE_TITLE_EN"
    };

    public static bool IsOptionalCustomerEnglishSource(string? source) =>
        source is not null && OptionalCustomerSources.Contains(source);

    public static bool IsOptionalCustomerEnglish(SoftwareSupplyPlaceholderDefinition field) =>
        field.IsSystem ? OptionalCustomerMarkers.Contains(field.Key) : IsOptionalCustomerEnglishSource(field.SourceFieldKey);

    public static string OptionalCustomerEnglish(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    public static string Date(DateTime value) => value.ToString("dd MMMM yyyy", CultureInfo.GetCultureInfo("en-GB"));

    public static string Require(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ContractTemplatePreviewException("EnglishSourceMissing",
                $"Thiếu {field}. Bổ sung tại Hồ sơ pháp lý/Khách hàng (hoặc bản nháp nghiệm thu) theo quyền được cấp, rồi tạo lại bản xem trước.");
        return value.Trim();
    }

    public static string Label(ContractLanguageMode mode, string english, string vietnamese) =>
        mode == ContractLanguageMode.Bilingual ? $"{english} / {vietnamese}" : vietnamese;

    public static IEnumerable<string?> Bodies(ContractLanguageMode language, byte mode, string? vietnamese, string? english)
    {
        if (language == ContractLanguageMode.Bilingual && mode == (byte)BilingualContentMode.Separate)
            yield return english;
        yield return vietnamese;
    }
}
