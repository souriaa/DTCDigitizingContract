using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using ContractManagement.API.Domains.Interfaces.Security;
using ContractManagement.API.Domains.Recovery;
using ContractManagement.API.Domains.Services.Security;
using ContractManagement.Infrastructure.MultiTenancy.Enums;
using ContractManagement.Infrastructure.MultiTenancy.Interfaces;
using ContractManagement.Infrastructure.MultiTenancy.Models;
using ContractManagement.Infrastructure.MultiTenancy.Options;
using ContractManagement.Infrastructure.MultiTenancy.Services;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using ContractManagement.Infrastructure.Persistence.Central;
using ContractManagement.Infrastructure.Persistence.Central.Entities;
using ContractManagement.Infrastructure.Persistence.Recovery;
using ContractManagement.API.Common.Enums;
using ContractManagement.Middleware.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ContractManagement.Tests.Integration;

// Real loopback HTTP, MVC, session cookies and production authorization/tenant middleware.
// Stores are isolated InMemory databases; SQL atomicity is covered separately by SQL QA tests.
public sealed class AccountRecoveryHttpTests
{
    [Fact]
    public async Task Verification_Reset_LoginAndOldSessionRevocation_WorkThroughHttp()
    {
        await using var host = await Host.Create();
        using var session = host.Client(cookies: true);
        using var publicClient = host.Client(cookies: false);
        Assert.Equal(HttpStatusCode.OK, (await session.PostAsJsonAsync("/api/system-auth/login", new { username = "qa-admin", password = "OriginalPassword1!" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/api/system-auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await publicClient.GetAsync("/api/system-auth/password-recovery/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/api/system-auth/password-recovery/health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await session.PostAsJsonAsync("/api/system-auth/recovery-email/request-verification", new { currentPassword = "OriginalPassword1!" })).StatusCode);
        var verification = await host.Link(RecoveryPurpose.VerifyRecoveryEmail);
        Assert.Equal(HttpStatusCode.OK, (await session.PostAsJsonAsync("/api/system-auth/recovery-email/verify", verification)).StatusCode);

        var unknown = await publicClient.PostAsJsonAsync("/api/system-auth/password-recovery/request", new { username = "unknown" });
        var known = await publicClient.PostAsJsonAsync("/api/system-auth/password-recovery/request", new { username = "qa-admin" });
        Assert.Equal(HttpStatusCode.Accepted, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, known.StatusCode);
        Assert.Equal(await unknown.Content.ReadAsStringAsync(), await known.Content.ReadAsStringAsync());
        Assert.Contains("no-store", known.Headers.CacheControl!.ToString());
        Assert.Equal("no-referrer", Assert.Single(known.Headers.GetValues("Referrer-Policy")));
        var link = await host.Link(RecoveryPurpose.PasswordReset);
        Assert.Equal(HttpStatusCode.OK, (await publicClient.PostAsJsonAsync("/api/system-auth/password-recovery/validate", link)).StatusCode);
        Assert.Null((await host.Challenge(RecoveryPurpose.PasswordReset)).UsedAtUtc);
        foreach (var invalid in new[] { "lowercase12345", "UPPERCASE12345", "MissingDigitsHere", "123456789012", "OriginalPassword1!" })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await publicClient.PostAsJsonAsync("/api/system-auth/password-recovery/reset", new {
                link.RequestId, link.Token, newPassword = invalid, confirmPassword = invalid
            })).StatusCode);
            Assert.Null((await host.Challenge(RecoveryPurpose.PasswordReset)).UsedAtUtc);
        }
        var reset = new { link.RequestId, link.Token, newPassword = "NewPassword123!", confirmPassword = "NewPassword123!" };
        Assert.Equal(HttpStatusCode.OK, (await publicClient.PostAsJsonAsync("/api/system-auth/password-recovery/reset", reset)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await publicClient.PostAsJsonAsync("/api/system-auth/password-recovery/reset", reset)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/system-auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.PostAsJsonAsync("/api/system-auth/login", new { username = "qa-admin", password = "OriginalPassword1!" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await session.PostAsJsonAsync("/api/system-auth/login", new { username = "qa-admin", password = "NewPassword123!" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/api/system-auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await session.PostAsync("/api/system-auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/system-auth/me")).StatusCode);
    }

    [Fact]
    public async Task TenantReset_DoesNotMutateCentralAccountOrSession_AndRejectsStaleTenantCookie()
    {
        await using var host = await Host.Create();
        using var session = host.Client(cookies: true);
        session.DefaultRequestHeaders.Add("X-Tenant-Code", "tenant-qa");
        Assert.Equal(HttpStatusCode.OK, (await session.PostAsJsonAsync("/api/auth/login", new { accountName = "qa-user", password = "OriginalPassword1!" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await session.PostAsJsonAsync("/api/system-auth/login", new { username = "qa-admin", password = "OriginalPassword1!" })).StatusCode);
        using var publicClient = host.Client(cookies: false);
        Assert.Equal(HttpStatusCode.Accepted, (await publicClient.PostAsJsonAsync("/api/auth/password-recovery/request", new { tenantCode = "tenant-qa", accountName = "qa-user" })).StatusCode);
        using (var scope = host.App.Services.CreateScope())
        {
            await using var db = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>().Create("qa");
            var challenge = await db.RecoveryChallenges.SingleAsync();
            var envelope = await db.RecoveryEmailOutbox.SingleAsync();
            var token = scope.ServiceProvider.GetRequiredService<AccountRecoveryCryptography>().Decrypt(envelope.TokenCiphertext!, "tenant:17");
            foreach (var invalid in new[] { "lowercase12345", "UPPERCASE12345", "MissingDigitsHere", "123456789012", "OriginalPassword1!" })
                Assert.Equal(HttpStatusCode.BadRequest, (await publicClient.PostAsJsonAsync("/api/auth/password-recovery/reset", new {
                    tenantCode = "tenant-qa", requestId = challenge.PublicId, token, newPassword = invalid, confirmPassword = invalid
                })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await publicClient.PostAsJsonAsync("/api/auth/password-recovery/reset", new {
                tenantCode = "tenant-qa", requestId = challenge.PublicId, token, newPassword = "NewPassword123!", confirmPassword = "NewPassword123!"
            })).StatusCode);
        }
        Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/api/system-auth/me")).StatusCode);
        // Existing employee validator clears the shared session when a stale tenant cookie is used.
        // Recovery itself does not clear it or modify Central; do not change unrelated auth semantics.
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await session.PostAsJsonAsync("/api/auth/login", new { accountName = "qa-user", password = "NewPassword123!" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task PublicTenantRouting_IgnoresConflictingHeader_RejectsWrongScopeAndInvalidDto()
    {
        await using var host = await Host.Create();
        using var client = host.Client(cookies: false);
        client.DefaultRequestHeaders.Add("X-Tenant-Code", "tenant-other");
        var accepted = await client.PostAsJsonAsync("/api/auth/password-recovery/request", new { tenantCode = "tenant-qa", accountName = "qa-user" });
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        using (var scope = host.App.Services.CreateScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>();
            await using var db = factory.Create("qa");
            var challenge = await db.Set<AccountRecoveryChallenge>().SingleAsync();
            var envelope = await db.Set<AccountRecoveryEmailOutbox>().SingleAsync();
            var token = scope.ServiceProvider.GetRequiredService<AccountRecoveryCryptography>().Decrypt(envelope.TokenCiphertext!, "tenant:17");
            var link = new { requestId = challenge.PublicId, token };
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/system-auth/password-recovery/validate", link)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/password-recovery/validate", new { link.requestId, link.token, tenantCode = "tenant-other" })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/password-recovery/validate", new { link.requestId, link.token, tenantCode = "tenant-qa" })).StatusCode);
            Assert.Null((await db.Set<AccountRecoveryChallenge>().SingleAsync()).UsedAtUtc);
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/password-recovery/request", new { tenantCode = "!", accountName = "qa-user" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/system-auth/password-recovery/validate", new { requestId = Guid.NewGuid(), token = "malformed" })).StatusCode);
    }

    private sealed class Host(WebApplication app) : IAsyncDisposable
    {
        public WebApplication App => app;
        public HttpClient Client(bool cookies) => new(new HttpClientHandler { UseCookies = cookies }) { BaseAddress = new(app.Urls.Single()) };
        public async Task<AccountRecoveryChallenge> Challenge(RecoveryPurpose purpose)
        {
            using var scope = app.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<CentralDbContext>().RecoveryChallenges.AsNoTracking().SingleAsync(x => x.Purpose == purpose);
        }
        public async Task<PasswordRecoveryLink> Link(RecoveryPurpose purpose)
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CentralDbContext>();
            var challenge = await db.RecoveryChallenges.SingleAsync(x => x.Purpose == purpose);
            var envelope = await db.RecoveryEmailOutbox.SingleAsync(x => x.RecoveryChallengeId == challenge.RecoveryChallengeId);
            return new() { RequestId = challenge.PublicId, Token = scope.ServiceProvider.GetRequiredService<AccountRecoveryCryptography>().Decrypt(envelope.TokenCiphertext!, "central") };
        }
        public static async Task<Host> Create()
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
            builder.Logging.ClearProviders();
            builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
            builder.Services.AddControllers().AddApplicationPart(typeof(SystemPasswordRecoveryController).Assembly);
            builder.Services.AddAuthorization();
            builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession();
            builder.Services.AddHttpContextAccessor();
            var database = Guid.NewGuid().ToString("N");
            builder.Services.AddDbContext<CentralDbContext>(o => o.UseInMemoryDatabase(database));
            builder.Services.AddScoped<ICentralSecurityAuditWriter, CentralSecurityAuditWriter>();
            builder.Services.AddScoped<ICurrentTenant, CurrentTenant>();
            builder.Services.Configure<MultiTenancyOptions>(_ => { });
            builder.Services.AddSingleton<ITenantResolver, Resolver>();
            builder.Services.AddSingleton<ITenantDbContextFactory, Factory>();
            builder.Services.AddScoped(p => p.GetRequiredService<ITenantDbContextFactory>()
                .Create(p.GetRequiredService<ICurrentTenant>().GetRequiredTenant().ConnectionString));
            builder.Services.AddSingleton<IPasswordHasher<SystemAdmin>, PasswordHasher<SystemAdmin>>();
            builder.Services.AddSingleton<IPasswordHasher<TblEmployee>, PasswordHasher<TblEmployee>>();
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddSingleton<IOptions<AccountRecoveryOptions>>(Options.Create(new AccountRecoveryOptions { Enabled = true, Provider = "Test", EncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) }));
            builder.Services.AddSingleton<AccountRecoveryCryptography>();
            builder.Services.AddSingleton<RecoveryTelemetry>();
            builder.Services.AddSingleton<RecoveryRequestLimiter>();
            builder.Services.AddScoped<PasswordRecoveryService>();
            builder.Services.AddScoped<RecoveryEmailVerificationService>();
            var app = builder.Build();
            app.UseRouting(); app.UseSession(); app.UseMiddleware<TenantResolutionMiddleware>(); app.UseAuthorization(); app.MapControllers();
            try
            {
                using var scope = app.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<CentralDbContext>();
                var admin = new SystemAdmin { Username = "qa-admin", FullName = "QA", Email = "qa@example.test", IsActive = true, SessionVersion = 1, RowVersion = new byte[8] };
                admin.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordHasher<SystemAdmin>>().HashPassword(admin, "OriginalPassword1!");
                db.Add(admin); await db.SaveChangesAsync();
                await using var tenantDb = scope.ServiceProvider.GetRequiredService<ITenantDbContextFactory>().Create("qa");
                var employee = new TblEmployee { EmployeeAccount = "qa-user", EmployeeFullName = "QA", EmployeeType = (byte)EmployeeType.Manager,
                    EmployeeEmail = "qa@example.test", Status = 1, SessionVersion = 1, RecoveryEmailVerifiedAt = DateTime.UtcNow, RowVersion = new byte[8] };
                employee.EmployeePassword = scope.ServiceProvider.GetRequiredService<IPasswordHasher<TblEmployee>>().HashPassword(employee, "OriginalPassword1!");
                tenantDb.Add(employee); await tenantDb.SaveChangesAsync();
                await app.StartAsync();
                return new(app);
            }
            catch { await app.DisposeAsync(); throw; }
        }
        public async ValueTask DisposeAsync() { await app.StopAsync(); await app.DisposeAsync(); }
    }
    private sealed class Resolver : ITenantResolver
    {
        public Task<ResolvedTenant?> ResolveAsync(string code, CancellationToken cancellationToken = default) =>
            Task.FromResult<ResolvedTenant?>(code == "tenant-qa" ? new(17, code, "QA", TenantDatabaseMode.Dedicated, "qa") : null);
    }
    private sealed class Factory : ITenantDbContextFactory
    {
        private readonly string name = Guid.NewGuid().ToString("N");
        public DbDtctechContext Create(string connectionString) => new(new DbContextOptionsBuilder<DbDtctechContext>().UseInMemoryDatabase(name).Options);
    }
}
