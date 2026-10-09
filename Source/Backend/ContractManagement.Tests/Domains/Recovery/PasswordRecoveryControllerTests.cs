using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using ContractManagement.API.Domains.Recovery;
using ContractManagement.Attributes;
using ContractManagement.Infrastructure.MultiTenancy.Enums;
using ContractManagement.Infrastructure.MultiTenancy.Interfaces;
using ContractManagement.Infrastructure.MultiTenancy.Models;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using ContractManagement.Infrastructure.Persistence.Central.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ContractManagement.Tests.Domains.Recovery;

public sealed class PasswordRecoveryControllerTests
{
    [Fact]
    public async Task TenantBRequest_DoesNotTouchTenantACookieHeaderOrSession_AndHasGenericTimingFloor()
    {
        using var f = new Fixture();
        await using var db = f.Factory.Create("tenant-B-connection");
        var employee = new TblEmployee { EmployeeId = 1, EmployeeAccount = "user", EmployeeEmail = "qa@example.test", Status = 1,
            RecoveryEmailVerifiedAt = DateTime.UtcNow, SessionVersion = 1, RowVersion = new byte[8] };
        employee.EmployeePassword = f.Services.GetRequiredService<IPasswordHasher<TblEmployee>>().HashPassword(employee, "Password123!");
        db.Add(employee); await db.SaveChangesAsync(); f.Factory.Calls.Clear();
        var timer = Stopwatch.StartNew();
        var known = Assert.IsType<ObjectResult>(await f.Controller.RequestRecovery(new() { TenantCode = "tenant-B", AccountName = "user" }, default));
        Assert.True(timer.ElapsedMilliseconds >= 200);
        Assert.Equal(202, known.StatusCode);
        Assert.Equal("tenant-B", f.Resolver.Codes.Single());
        Assert.Equal("tenant-B-connection", f.Factory.Calls.Single());
        Assert.Equal("tenant-A", f.Http.Session.GetString("TenantCode"));
        Assert.Equal(100, f.Http.Session.GetInt32("EmployeeId"));
        Assert.Equal("tenant-A", f.Http.Request.Headers["X-Tenant-Code"]);
        Assert.Equal("no-store", f.Http.Response.Headers.CacheControl);
        f.Factory.Calls.Clear(); timer.Restart();
        var unknown = Assert.IsType<ObjectResult>(await f.Controller.RequestRecovery(new() { TenantCode = "missing", AccountName = "user" }, default));
        Assert.True(timer.ElapsedMilliseconds >= 200);
        Assert.Equal(202, unknown.StatusCode); Assert.Equal(known.Value, unknown.Value); Assert.Empty(f.Factory.Calls);
        Assert.Equal(1, await db.Set<ContractManagement.Infrastructure.Persistence.Recovery.AccountRecoveryChallenge>().CountAsync());
        Assert.True(Attribute.IsDefined(typeof(TenantPasswordRecoveryController), typeof(AllowWithoutTenantAttribute)));
    }

    [Fact]
    public async Task UnknownRequestsAlsoConsumeIpQuota_ReturnRetryAfter_AndDisabledNeverOpensStore()
    {
        using var f = new Fixture();
        f.Settings.RequestsPerIpPerHour = 1;
        Assert.Equal(202, Assert.IsType<ObjectResult>(await f.Controller.RequestRecovery(new() { TenantCode = "missing", AccountName = "unknown" }, default)).StatusCode);
        Assert.Equal(429, Assert.IsType<ObjectResult>(await f.Controller.RequestRecovery(new() { TenantCode = "missing", AccountName = "unknown" }, default)).StatusCode);
        Assert.True(int.Parse(f.Http.Response.Headers.RetryAfter!) > 0);
        Assert.Empty(f.Factory.Calls);
        f.Settings.Enabled = false; f.Resolver.Codes.Clear();
        Assert.Equal(503, Assert.IsType<ObjectResult>(await f.Controller.RequestRecovery(new() { TenantCode = "tenant-B", AccountName = "user" }, default)).StatusCode);
        Assert.Empty(f.Resolver.Codes); Assert.Empty(f.Factory.Calls);
    }

    [Fact]
    public async Task DatabaseFailure_IsSanitizedOperationalError_NotFakeAcceptance()
    {
        using var f = new Fixture(); f.Factory.Fail = true;
        var result = Assert.IsType<ObjectResult>(await f.Controller.RequestRecovery(new() { TenantCode = "tenant-B", AccountName = "user" }, default));
        Assert.Equal(503, result.StatusCode);
        var body = JsonSerializer.Serialize(result.Value);
        Assert.Contains("PasswordRecoveryUnavailable", body); Assert.Contains("correlationId", body);
        Assert.DoesNotContain("SECRET", body);
    }

    private sealed class Fixture : IDisposable
    {
        public readonly ServiceProvider Services = new ServiceCollection()
            .AddSingleton<IPasswordHasher<TblEmployee>, PasswordHasher<TblEmployee>>()
            .AddSingleton<IPasswordHasher<SystemAdmin>, PasswordHasher<SystemAdmin>>().BuildServiceProvider();
        public readonly AccountRecoveryOptions Settings = new() { Enabled = true, Provider = "Test", EncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) };
        public readonly Resolver Resolver = new(); public readonly Factory Factory = new();
        public readonly DefaultHttpContext Http = new() { Session = new TestSession() };
        public readonly RecoveryTelemetry Telemetry;
        public readonly TenantPasswordRecoveryController Controller;
        public Fixture()
        {
            Http.Session.SetString("TenantCode", "tenant-A"); Http.Session.SetInt32("EmployeeId", 100);
            Http.Request.Headers["X-Tenant-Code"] = "tenant-A"; Http.Request.Path = "/api/auth/password-recovery/request";
            var options = Options.Create(Settings); var crypto = new AccountRecoveryCryptography(options);
            var service = new PasswordRecoveryService(Services, options, crypto, TimeProvider.System, new HttpContextAccessor { HttpContext = Http });
            Telemetry = new(options);
            Controller = new(Resolver, Factory, service, new(TimeProvider.System, options), Telemetry, NullLogger<TenantPasswordRecoveryController>.Instance) {
                ControllerContext = new() { HttpContext = Http }
            };
        }
        public void Dispose() { Services.Dispose(); Telemetry.Dispose(); }
    }
    private sealed class Resolver : ITenantResolver
    {
        public List<string> Codes = new();
        public Task<ResolvedTenant?> ResolveAsync(string code, CancellationToken cancellationToken = default)
        {
            Codes.Add(code);
            return Task.FromResult<ResolvedTenant?>(code == "tenant-B" ? new(2, "tenant-B", "QA", TenantDatabaseMode.Dedicated, "tenant-B-connection") : null);
        }
    }
    private sealed class Factory : ITenantDbContextFactory
    {
        private readonly string name = Guid.NewGuid().ToString(); public bool Fail; public List<string> Calls = new();
        public DbDtctechContext Create(string connectionString)
        {
            if (Fail) throw new Exception("SECRET connection string/token must never escape.");
            Calls.Add(connectionString);
            return new(new DbContextOptionsBuilder<DbDtctechContext>().UseInMemoryDatabase(name).Options);
        }
    }
    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> values = new();
        public bool IsAvailable => true; public string Id => "qa"; public IEnumerable<string> Keys => values.Keys;
        public void Clear() => values.Clear(); public void Remove(string key) => values.Remove(key);
        public void Set(string key, byte[] value) => values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => values.TryGetValue(key, out value!);
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
