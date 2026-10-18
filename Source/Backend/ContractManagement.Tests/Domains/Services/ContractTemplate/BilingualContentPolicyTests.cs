using System.Text.Json;
using ContractManagement.API.Common.Enums;
using ContractManagement.API.Domains.DTOs.Requests.Contract;
using ContractManagement.Domains.Policies.ContractTemplate;
using static ContractManagement.Tests.ContractRichTextTestData;

namespace ContractManagement.Tests.Domains.Services.ContractTemplate;

public sealed class BilingualContentPolicyTests
{
    [Theory]
    [InlineData(true, "tiếng Anh")]
    [InlineData(false, "tiếng Việt")]
    public void SeparateError_IdentifiesItemAndMissingLanguage(bool missingEnglish, string language)
    {
        var error = Assert.Throws<ArgumentException>(() => BilingualContentPolicy.Validate(1,
            ContractLanguageMode.Bilingual, "Scope",
            missingEnglish ? RichText("Nội dung Việt") : null,
            missingEnglish ? RichText(" ") : RichText("English body"),
            "Điều khoản 'Phạm vi cung cấp' (mã TERM-03)"));
        Assert.Contains("Phạm vi cung cấp", error.Message);
        Assert.Contains("TERM-03", error.Message);
        Assert.Contains("thiếu nội dung " + language, error.Message);
    }

    [Fact]
    public void InvalidRichTextError_IdentifiesItemAndLanguage()
    {
        var error = Assert.Throws<ArgumentException>(() => BilingualContentPolicy.Validate(1,
            ContractLanguageMode.Bilingual, "Scope", RichText("Nội dung Việt"), "invalid-rich-text",
            "Điều khoản 'Phạm vi' (mã TERM-03)"));
        Assert.Contains("TERM-03", error.Message);
        Assert.Contains("nội dung tiếng Anh", error.Message);
        Assert.Contains("trình soạn thảo", error.Message);
    }

    [Fact]
    public void Combined_RequiresOneBody_AndDoesNotRequireEnglishBody()
    {
        BilingualContentPolicy.Validate(2, ContractLanguageMode.Bilingual,
            "Payment", RichText("English / Tiếng Việt"), null, requireBody: true);
        Assert.Throws<ArgumentException>(() => BilingualContentPolicy.Validate(2,
            ContractLanguageMode.Bilingual, "Payment", RichText("Body"), RichText("Other")));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(255)]
    public void InvalidMode_IsRejected(byte mode) =>
        Assert.Throws<ArgumentException>(() => BilingualContentPolicy.Validate(mode,
            ContractLanguageMode.Bilingual, "Title", null, null));

    [Fact]
    public void Combined_OnVietnameseDocument_AndMissingEnglishTitle_AreRejected()
    {
        Assert.Throws<ArgumentException>(() => BilingualContentPolicy.Validate(2,
            ContractLanguageMode.Vietnamese, "Title", RichText("Body"), null));
        Assert.Throws<ArgumentException>(() => BilingualContentPolicy.Validate(2,
            ContractLanguageMode.Bilingual, " ", RichText("Body"), null));
    }

    [Fact]
    public void Separate_RecognizesEncodedEmptyParagraphs()
    {
        Assert.False(BilingualContentPolicy.HasContent(RichText("  ")));
        Assert.Throws<ArgumentException>(() => BilingualContentPolicy.Validate(1,
            ContractLanguageMode.Bilingual, "Title", RichText("Body"), RichText("  ")));
        BilingualContentPolicy.Validate(1, ContractLanguageMode.Bilingual,
            "Title", RichText("Nội dung"), RichText("Content"));
    }

    [Fact]
    public void IncomingTermPayload_MustExplicitlyContainMode()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CreateContractTermRequest>(
            """{"TermKind":0,"PaymentMilestones":[]}"""));
        Assert.Equal(2, JsonSerializer.Deserialize<CreateContractTermRequest>(
            """{"TermKind":0,"PaymentMilestones":[],"BilingualContentMode":2}""")!.BilingualContentMode);
    }
}
