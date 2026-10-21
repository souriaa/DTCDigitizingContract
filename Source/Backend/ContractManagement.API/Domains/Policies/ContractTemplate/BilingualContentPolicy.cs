using ContractManagement.API.Common.Enums;

namespace ContractManagement.Domains.Policies.ContractTemplate;

public static class BilingualContentPolicy
{
    public static bool HasContent(string? value, string label = "Nội dung")
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (!ContractTermRichText.TryParse(value, out var document))
            throw new ArgumentException($"{label}: định dạng soạn thảo không hợp lệ. Hãy mở mục này và lưu lại nội dung bằng trình soạn thảo.");
        return document.Blocks.Any(block => block.Type == "table"
            || block.Runs?.Any(run => !string.IsNullOrWhiteSpace(run.Text)) == true);
    }

    public static void Validate(byte mode, ContractLanguageMode language,
        string? titleEn, string? content, string? contentEn,
        string label = "Điều khoản", bool requireBody = false)
    {
        if (mode is not (byte)BilingualContentMode.Separate and not (byte)BilingualContentMode.Combined)
            throw new ArgumentException($"{label}: chế độ nội dung không hợp lệ.");
        var hasMain = HasContent(content, $"{label} — nội dung {(mode == (byte)BilingualContentMode.Combined ? "song ngữ" : "tiếng Việt")}");
        var hasEnglish = HasContent(contentEn, $"{label} — nội dung tiếng Anh");
        if (mode == (byte)BilingualContentMode.Combined)
        {
            if (language != ContractLanguageMode.Bilingual)
                throw new ArgumentException($"{label}: chỉ tài liệu song ngữ được gộp nội dung.");
            if (!string.IsNullOrWhiteSpace(contentEn))
                throw new ArgumentException($"{label}: nội dung gộp không được có nội dung tiếng Anh riêng.");
        }
        if (requireBody && !hasMain)
            throw new ArgumentException($"{label}: nội dung không được để trống.");
        if (language == ContractLanguageMode.Bilingual)
        {
            if (string.IsNullOrWhiteSpace(titleEn))
                throw new ArgumentException($"{label}: tiêu đề tiếng Anh không được để trống.");
            if (mode == (byte)BilingualContentMode.Separate && (hasMain || hasEnglish || requireBody)
                && (!hasMain || !hasEnglish))
            {
                var missingLanguage = !hasMain && !hasEnglish ? "tiếng Anh và tiếng Việt"
                    : !hasEnglish ? "tiếng Anh" : "tiếng Việt";
                throw new ArgumentException($"{label}: thiếu nội dung {missingLanguage} trong chế độ tách riêng. Hãy bổ sung nội dung hoặc chọn gộp nội dung song ngữ nếu chỉ soạn một bản chung.");
            }
        }
    }
}
