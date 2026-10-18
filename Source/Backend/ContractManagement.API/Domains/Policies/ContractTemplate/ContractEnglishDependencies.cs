using ContractManagement.API.Common.Enums;
using ContractManagement.Domains.Interfaces.ContractTemplate;
using ContractManagement.Infrastructure.Persistence.Application.Models;

namespace ContractManagement.Domains.Policies.ContractTemplate;

public sealed record MissingDocumentEnglishSource(string Area, string Label)
{
    public string Code => $"EnglishSourceMissing{char.ToUpperInvariant(Area[0])}{Area[1..]}";
}

/// <summary>Works only with the DOCX field snapshot and preloaded sources, without database queries.</summary>
public static class ContractEnglishDependencies
{
    private static readonly IReadOnlyDictionary<string, string> Sources = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["CONTRACT_NAME_EN"] = "contract.name-en",
        ["CONTRACT_DATE_EN"] = "contract.created-date-en",
        ["EFFECTIVE_DATE_EN"] = "contract.effective-date-en",
        ["EXPIRE_DATE_EN"] = "contract.expire-date-en",
        ["CONTRACT_TOTAL_AMOUNT_IN_WORDS_EN"] = "version.total-amount-in-words-en",
        ["CUSTOMER_COMPANY_EN"] = "customer.company-en",
        ["CUSTOMER_ADDRESS_EN"] = "customer.address-en",
        ["CUSTOMER_BANK_NAME_EN"] = "customer.bank-name-en",
        ["CUSTOMER_CITY_EN"] = "customer.city-en",
        ["CUSTOMER_COUNTRY_EN"] = "customer.country-en",
        ["CUSTOMER_CONTACT_TITLE_EN"] = "customer.contact-title-en",
        ["PROVIDER_LEGAL_NAME_EN"] = "provider.legal-name-en",
        ["PROVIDER_ADDRESS_EN"] = "provider.address-en",
        ["PROVIDER_REPRESENTATIVE_TITLE_EN"] = "provider.representative-title-en",
        ["PROVIDER_BANK_NAME_EN"] = "provider.bank-name-en"
    };

    public static IReadOnlyList<MissingDocumentEnglishSource> FindMissing(
        IReadOnlyList<TblContractTemplateField> usedFields, ContractLanguageMode language,
        bool hasAppendices, ContractPlaceholderResolveContext context,
        IContractPlaceholderSourceRegistry registry)
    {
        var required = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in usedFields)
        {
            if (field.IsSystem && Sources.TryGetValue(field.PlaceholderKey, out var source)) required.Add(source);
            if (!field.IsSystem && field.SourceFieldKey.EndsWith("-en", StringComparison.Ordinal)) required.Add(field.SourceFieldKey);
        }
        if (language == ContractLanguageMode.Bilingual && hasAppendices
            && usedFields.Any(x => x.PlaceholderKey == "CONTRACT_APPENDICES"))
        {
            required.Add("contract.name-en");
            required.Add("provider.legal-name-en");
        }
        var result = new List<MissingDocumentEnglishSource>();
        foreach (var key in required)
        {
            if (DocumentEnglishPolicy.IsOptionalCustomerEnglishSource(key)) continue;
            var raw = registry.Resolve(key, context);
            if (raw is null || raw is string text && string.IsNullOrWhiteSpace(text))
                result.Add(new(key.Split('.')[0], registry.GetRequired(key).FieldLabel));
        }
        return result;
    }
}
