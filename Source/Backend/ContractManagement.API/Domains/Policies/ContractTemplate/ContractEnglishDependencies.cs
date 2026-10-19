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
    private static readonly IReadOnlyDictionary<string, string> Sources =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["CONTRACT_NAME_EN"] = "contract.name-en",
            ["CONTRACT_DATE_EN"] = "contract.created-date-en",
            ["EFFECTIVE_DATE_EN"] = "contract.effective-date-en",
            ["EXPIRE_DATE_EN"] = "contract.expire-date-en",
            ["CONTRACT_TOTAL_AMOUNT_IN_WORDS_EN"] = "version.total-amount-in-words-en"
        }.Concat(DocumentPartyMarkers.Definitions.Where(x => x.Key.EndsWith("_EN", StringComparison.Ordinal))
            .Select(x => new KeyValuePair<string, string>(x.Key,
                (x.Key.StartsWith("PROVIDER_", StringComparison.Ordinal) ? "provider." : "customer.")
                + DocumentPartyMarkers.SourceSuffix(x.Key[(x.Key.IndexOf('_') + 1)..]))))
        .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

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
            required.Add("tenant.legal-name-en");
        }
        var result = new List<MissingDocumentEnglishSource>();
        foreach (var key in required)
        {
            if (DocumentEnglishPolicy.IsOptionalPartyEnglish(new(key, key, false, TemplatePlaceholderDataKind.Scalar,
                    TemplatePlaceholderMultiplicity.ZeroOrMore, key) { IsSystem = false, SourceFieldKey = key },
                    (ContractDirection)context.Contract.ContractDirection)) continue;
            var raw = registry.Resolve(key, context);
            if (raw is null || raw is string text && string.IsNullOrWhiteSpace(text))
                result.Add(new(key.Split('.')[0], registry.GetRequired(key).FieldLabel));
        }
        return result;
    }
}
