using System.Text.Json;
using ContractManagement.Domains.Interfaces.ContractTemplate;
using ContractManagement.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace ContractManagement.Tests.Domains.Services.ContractTemplate;

public sealed class DocumentEnglishApiErrorTests
{
    [Fact]
    public async Task MissingTranslation_ReturnsActionableCodeAndMessage_NotGenericError()
    {
        var middleware = new ExceptionHandlingMiddleware(_ => throw new ContractTemplatePreviewException(
            "EnglishSourceMissing", "Thiếu tên ngân hàng tiếng Anh. Bổ sung Hồ sơ pháp lý."),
            NullLogger<ExceptionHandlingMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(context);
        Assert.Equal(400, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("EnglishSourceMissing", json.RootElement.GetProperty("code").GetString());
        Assert.Contains("Hồ sơ pháp lý", json.RootElement.GetProperty("message").GetString());
    }
}
