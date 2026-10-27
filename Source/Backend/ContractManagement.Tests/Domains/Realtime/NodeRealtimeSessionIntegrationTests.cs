using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ContractManagement.API.Common.Security;
using ContractManagement.API.Domains.Realtime;
using ContractManagement.Filter;
using ContractManagement.Infrastructure.MultiTenancy.Enums;
using ContractManagement.Infrastructure.MultiTenancy.Interfaces;
using ContractManagement.Infrastructure.MultiTenancy.Models;
using ContractManagement.Infrastructure.MultiTenancy.Options;
using ContractManagement.Infrastructure.MultiTenancy.Services;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using ContractManagement.Middleware.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ContractManagement.Tests.Domains.Realtime;

public sealed class NodeRealtimeSessionIntegrationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealCookieSession_LogoutInvalidatesLease_WithoutGivingCookieToNode(bool throughSameOriginProxy)
    {
        // Isolated Kestrel + InMemory DB; no application process, real DB, migration or email.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development", Args = [] });
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0));
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
            options.KnownProxies.Add(IPAddress.Loopback);
        });
        builder.Services.AddHttpsRedirection(options => options.HttpsPort = 443);
        builder.Services.AddDistributedMemoryCache();
        builder.Services.AddSession(options => options.Cookie.Name = "ContractManagement.Session");
        builder.Services.AddControllers().AddApplicationPart(typeof(NodeRealtimeController).Assembly);
        builder.Services.Configure<MultiTenancyOptions>(_ => { });
        builder.Services.AddSingleton<ITenantResolver, Resolver>();
        builder.Services.AddScoped<ICurrentTenant, CurrentTenant>();
        var dbName = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<DbDtctechContext>(options => options.UseInMemoryDatabase(dbName));
        var settings = new NodeRealtimeOptions { Enabled = true, ServiceSecret = "integration-test-secret-at-least-32-characters" };
        builder.Services.AddSingleton<IOptions<NodeRealtimeOptions>>(Options.Create(settings));
        builder.Services.AddSingleton<RealtimeServiceAuthentication>();
        builder.Services.AddSingleton<NodeRealtimeBroker>();
        builder.Services.AddHttpClient("RealtimeInternal").ConfigurePrimaryHttpMessageHandler(() =>
            new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false, UseProxy = false });
        await using var app = builder.Build();
        app.UseForwardedHeaders();
        app.UseHttpsRedirection();
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/internal/realtime")) context.Request.EnableBuffering(16384, 16384);
            await next(context);
        });
        app.UseRouting();
        app.UseSession();
        app.UseMiddleware<TenantResolutionMiddleware>();
        app.MapPost("/test/login", (HttpContext http) =>
        {
            http.Session.SetString("TenantCode", "tenant-a");
            http.Session.SetInt32("TenantId", 10);
            http.Session.SetInt32(AccountSessionKeys.EmployeeId, 12);
            http.Session.SetInt32(AccountSessionKeys.EmployeeSessionVersion, 1);
            return Results.Ok();
        });
        app.MapPost("/api/test/logout", (HttpContext http) => { http.Session.Clear(); return Results.Ok(); })
            .WithMetadata(new SessionAuthorizeAttribute());
        app.MapControllers();
        await app.StartAsync();
        settings.ApiBaseUrl = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DbDtctechContext>();
            db.TblEmployees.Add(new TblEmployee { EmployeeId = 12, EmployeeAccount = "manager", EmployeeFullName = "Manager",
                EmployeeType = 6, Status = 1, SessionVersion = 1 });
            await db.SaveChangesAsync();
        }
        using var browser = new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = new Uri(settings.ApiBaseUrl) };
        var browserOrigin = throughSameOriginProxy
            ? settings.ApiBaseUrl.Replace("http://", "https://", StringComparison.Ordinal) : "http://localhost:3000";
        browser.DefaultRequestHeaders.Add("Origin", browserOrigin);
        browser.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");
        (await browser.PostAsync("/test/login", null)).EnsureSuccessStatusCode();
        browser.DefaultRequestHeaders.Remove("Origin");
        foreach (var deniedOrigin in new[] { null, "null", "http://evil.test", "https://127.0.0.1:1" })
        {
            using var blocked = new HttpRequestMessage(HttpMethod.Post, "/api/realtime/notifications/ticket");
            if (deniedOrigin is not null) blocked.Headers.Add("Origin", deniedOrigin);
            using var rejection = await browser.SendAsync(blocked);
            Assert.Equal(HttpStatusCode.Forbidden, rejection.StatusCode);
            Assert.Equal("RealtimeOriginNotAllowed", (await rejection.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        }
        browser.DefaultRequestHeaders.Add("Origin", browserOrigin);
        settings.Enabled = false;
        using (var disabled = await browser.PostAsync("/api/realtime/notifications/ticket", null))
        {
            Assert.Equal(HttpStatusCode.Forbidden, disabled.StatusCode);
            Assert.Equal("NodeRealtimeDisabled", (await disabled.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        }
        settings.Enabled = true;
        using (var unsignedProbe = await browser.GetAsync("/api/realtime/notifications/probe"))
        {
            Assert.Equal(HttpStatusCode.Forbidden, unsignedProbe.StatusCode);
            Assert.Equal("RealtimeServiceAuthenticationInvalid", (await unsignedProbe.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        }
        var ticketResponse = await browser.PostAsync("/api/realtime/notifications/ticket", null);
        ticketResponse.EnsureSuccessStatusCode();
        var ticket = (await ticketResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("ticket").GetString()!;
        var authentication = app.Services.GetRequiredService<RealtimeServiceAuthentication>();
        using var node = new HttpClient(new HttpClientHandler { UseCookies = false, UseProxy = false }) { BaseAddress = browser.BaseAddress };
        async Task<HttpResponseMessage> Internal(string action, object payload)
        {
            var path = $"/internal/realtime/{action}";
            var body = JsonSerializer.Serialize(payload);
            using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            authentication.Sign(request, path, body);
            return await node.SendAsync(request);
        }
        using var consume = await Internal("consume", new { ticket, origin = browserOrigin });
        consume.EnsureSuccessStatusCode();
        var lease = (await consume.Content.ReadFromJsonAsync<NodeSubscription>())!;
        Assert.Equal("tenant:10:employee:12", lease.Room);
        Assert.DoesNotContain("Cookie", await consume.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        using var live = await Internal("validate", new { leaseId = lease.LeaseId });
        live.EnsureSuccessStatusCode();
        (await browser.PostAsync("/api/test/logout", null)).EnsureSuccessStatusCode();
        using var revoked = await Internal("validate", new { leaseId = lease.LeaseId });
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
        using var replay = await Internal("consume", new { ticket, origin = browserOrigin });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        await app.StopAsync();
    }

    private sealed class Resolver : ITenantResolver
    {
        public Task<ResolvedTenant?> ResolveAsync(string tenantCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<ResolvedTenant?>(tenantCode == "tenant-a"
                ? new ResolvedTenant(10, tenantCode, "Test tenant", TenantDatabaseMode.Dedicated, "InMemory") : null);
    }
}
