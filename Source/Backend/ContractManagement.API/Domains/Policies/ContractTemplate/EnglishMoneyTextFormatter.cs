using ContractManagement.Domains.Interfaces.ContractTemplate;

namespace ContractManagement.Domains.Policies.ContractTemplate;

/// <summary>Deterministic decimal formatter; never translates or converts a currency.</summary>
public static class EnglishMoneyTextFormatter
{
    private static readonly string[] Small = ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen"];
    private static readonly string[] Tens = ["", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];
    private static readonly string[] Scales = ["", "thousand", "million", "billion", "trillion", "quadrillion", "quintillion", "sextillion", "septillion", "octillion"];

    public static string Format(decimal amount, string currency)
    {
        currency = currency.Trim().ToUpperInvariant();
        if (currency is not ("VND" or "USD"))
            throw new ContractTemplatePreviewException("EnglishCurrencyUnsupported", "Số tiền bằng chữ tiếng Anh chỉ hỗ trợ VND và USD.");
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        var rounded = decimal.Round(amount, currency == "VND" ? 0 : 2, MidpointRounding.AwayFromZero);
        var whole = decimal.Truncate(rounded);
        var text = Words(whole) + (currency == "VND" ? " Vietnamese dong" : whole == 1 ? " US dollar" : " US dollars");
        var cents = (int)((rounded - whole) * 100);
        if (currency == "USD" && cents != 0)
            text += " and " + Words(cents) + (cents == 1 ? " cent" : " cents");
        else text += " only";
        return char.ToUpperInvariant(text[0]) + text[1..];
    }

    private static string Words(decimal value)
    {
        if (value == 0) return Small[0];
        var groups = new List<string>();
        for (var scale = 0; value > 0; scale++, value = decimal.Truncate(value / 1000))
        {
            var group = (int)(value % 1000);
            if (group == 0) continue;
            var words = group >= 100 ? Small[group / 100] + " hundred" : "";
            var rest = group % 100;
            if (rest != 0)
                words += (words.Length == 0 ? "" : " and ") + (rest < 20 ? Small[rest] : Tens[rest / 10] + (rest % 10 == 0 ? "" : "-" + Small[rest % 10]));
            groups.Insert(0, words + (scale == 0 ? "" : " " + Scales[scale]));
        }
        return string.Join(" ", groups);
    }
}
