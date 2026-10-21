using System.Globalization;
using ContractManagement.Domains.Interfaces.ContractTemplate;
using ContractManagement.API.Common.Enums;

namespace ContractManagement.Domains.Policies.ContractTemplate;

/// <summary>Language order and strict English dependencies shared by both document renderers.</summary>
public static class DocumentEnglishPolicy
{
    private static readonly HashSet<string> OptionalCustomerSources = new(StringComparer.Ordinal)
    {
        "counterparty.company-en", "counterparty.address-en", "counterparty.bank-name-en", "counterparty.city-en",
        "counterparty.country-en", "counterparty.contact-title-en", "counterparty.representative-title-en"
    };

    public static bool IsOptionalCustomerEnglishSource(string? source) =>
        source is not null && OptionalCustomerSources.Contains(source);

    public static bool IsOptionalPartyEnglish(SoftwareSupplyPlaceholderDefinition field, ContractDirection direction)
    {
        if (IsOptionalCustomerEnglishSource(field.SourceFieldKey)) return true;
        if (field.IsSystem && new[] { "_CITY_EN", "_COUNTRY_EN", "_CONTACT_TITLE_EN" }.Any(x => field.Key.EndsWith(x, StringComparison.Ordinal))) return true;
        if (!field.IsSystem && field.SourceFieldKey is { } key && new[] { ".city-en", ".country-en", ".contact-title-en" }.Any(x => key.EndsWith(x, StringComparison.Ordinal))) return true;
        var externalRole = direction == ContractDirection.Purchase ? "PROVIDER" : "CUSTOMER";
        return field.IsSystem
            ? DocumentPartyMarkers.Fields.Any(x => x.Suffix.EndsWith("_EN", StringComparison.Ordinal)
                && field.Key == externalRole + "_" + x.Suffix)
            : field.SourceFieldKey?.StartsWith(externalRole.ToLowerInvariant() + ".", StringComparison.Ordinal) == true
                && field.SourceFieldKey.EndsWith("-en", StringComparison.Ordinal);
    }

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

    public static string SignatureHeading(ContractLanguageMode mode, bool customer) =>
        customer
            ? Label(mode, "CUSTOMER REPRESENTATIVE", "ĐẠI DIỆN BÊN KHÁCH HÀNG")
            : Label(mode, "PROVIDER REPRESENTATIVE", "ĐẠI DIỆN BÊN CUNG CẤP");

    public static IEnumerable<string?> Bodies(ContractLanguageMode language, byte mode, string? vietnamese, string? english)
    {
        if (language == ContractLanguageMode.Bilingual && mode == (byte)BilingualContentMode.Separate)
            yield return english;
        yield return vietnamese;
    }
}
