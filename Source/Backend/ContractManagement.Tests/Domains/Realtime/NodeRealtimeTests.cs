using System.Net;
using System.Net.Http.Json;
using System.Text;
using ContractManagement.API.Domains.Realtime;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Options;
using ContractManagement.API.Common.Security;
using ContractManagement.API.Domains.Discussions;
using ContractManagement.API.Domains.Notifications;
using ContractManagement.Domains.Services.Contract;
using ContractManagement.Infrastructure.MultiTenancy.Enums;
using ContractManagement.Infrastructure.MultiTenancy.Models;
using ContractManagement.Infrastructure.MultiTenancy.Services;

namespace ContractManagement.Tests.Domains.Realtime;

public sealed class NodeRealtimeTests
{
    private const string Secret = "test-only-realtime-service-secret-32-characters";
    private const string Origin = "http://localhost:3000";
    private static NodeSubscription Scope() => new("", "tenant:10:employee:12", "employee:10:12", DateTime.UtcNow.AddMinutes(15));
    private static IOptions<NodeRealtimeOptions> Options() => Microsoft.Extensions.Options.Options.Create(
        new NodeRealtimeOptions { Enabled = true, ServiceSecret = Secret });

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(6, true)]
    public async Task DiscussionAuthorization_ReusesExistingRoleAndOwnershipChecks(byte role, bool tenantAccess)
    {
        await using var db = new DbDtctechContext(new DbContextOptionsBuilder<DbDtctechContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.TblEmployees.Add(new TblEmployee { EmployeeId = 12, EmployeeAccount = "employee", EmployeeFullName = "Employee",
            EmployeeType = role, Status = 1, SessionVersion = 1 });
        db.TblContracts.Add(new TblContract { ContractId = 20, ContractDirection = 1, ContractName = "Test", EmployeeId = 13 });
        db.TblContractVersions.Add(new TblContractVersion { VersionId = 30, ContractId = 20 });
        await db.SaveChangesAsync();
        var tenant = new CurrentTenant();
        tenant.Set(new ResolvedTenant(10, "tenant-a", "Test", TenantDatabaseMode.Dedicated, "InMemory"));
        var http = new DefaultHttpContext { Session = new MemorySession() };
        http.Session.SetInt32("TenantId", 10);
        http.Session.SetInt32(AccountSessionKeys.EmployeeId, 12);
        http.Session.SetInt32(AccountSessionKeys.EmployeeSessionVersion, 1);
        http.Request.RouteValues["contractId"] = "20";
        http.Request.RouteValues["versionId"] = "30";
        var access = new ContractDiscussionAccessService(tenant, db, null!);
        if (tenantAccess) Assert.Equal(20, (await access.ForEmployeeAsync(http)).ContractId);
        else await Assert.ThrowsAsync<KeyNotFoundException>(() => access.ForEmployeeAsync(http));
        db.TblContracts.Single().EmployeeId = 12;
        await db.SaveChangesAsync();
        Assert.Equal(30, (await access.ForEmployeeAsync(http)).VersionId);
        http.Session.SetInt32(AccountSessionKeys.EmployeeSessionVersion, 0);
        await Assert.ThrowsAsync<RbacOperationException>(() => access.ForEmployeeAsync(http));
    }

    [Theory]
    [InlineData(true, "Pending")]
    [InlineData(false, "Disabled")]
    public async Task NotificationStage_KeepsNodeStateAtomicAndSeparateFromSignalR(bool enabled, string expected)
    {
        await using var db = new DbDtctechContext(new DbContextOptionsBuilder<DbDtctechContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.TblEmployees.Add(new TblEmployee { EmployeeId = 12, EmployeeAccount = "employee", EmployeeType = 1, Status = 1 });
        db.TblContracts.Add(new TblContract { ContractId = 20, ContractDirection = 1, EmployeeId = 12, ContractName = "Test" });
        await db.SaveChangesAsync();
        var settings = Options(); settings.Value.Enabled = enabled;
        var publisher = new NotificationPublisher(db,
            [new ContractNotificationResourcePolicy(db, new ContractResourceAuthorizationService(db))], settings);
        Assert.True(await publisher.StageAsync(new("node:test:1", "Contract", "Test", "Contract", 20, null,
            "Test", null, DateTime.UtcNow, "overview"), [12]));
        Assert.Empty(await db.TblNotificationDispatchOutbox.AsNoTracking().ToListAsync());
        await db.SaveChangesAsync();
        var row = await db.TblNotificationDispatchOutbox.SingleAsync();
        Assert.Equal("Pending", row.Status);
        Assert.Equal(expected, row.NodeStatus);
        Assert.Equal(enabled, row.NodeNextAttemptAtUtc.HasValue);
        row.Status = "Dispatched";
        Assert.Equal(expected, row.NodeStatus);
    }

    [Fact]
    public async Task Ticket_ConcurrentConsumptionIsAtomic_AndProbeRetainsCookieOnlyInsideDotnet()
    {
        var factory = new Factory();
        var broker = new NodeRealtimeBroker(factory, Options(), new(Options()));
        var ticket = broker.Issue("ContractManagement.Session=secret-session-cookie", "/api/realtime/notifications/probe", Origin, Scope());
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => broker.ConsumeAsync(ticket, Origin, default)));
        var scope = Assert.Single(results, x => x is not null)!;
        Assert.Equal("tenant:10:employee:12", scope.Room);
        Assert.DoesNotContain("secret-session-cookie", System.Text.Json.JsonSerializer.Serialize(scope));
        Assert.Equal("ContractManagement.Session=secret-session-cookie", Assert.Single(factory.Cookies));
        Assert.Equal("/api/realtime/notifications/probe", Assert.Single(factory.Paths));
        Assert.NotNull(await broker.ValidateAsync(scope.LeaseId, default));
        broker.Release(scope.LeaseId);
        Assert.Null(await broker.ValidateAsync(scope.LeaseId, default));
    }

    [Fact]
    public async Task Lease_RevokedSessionOrChangedScopeFailsClosed_AndCannotBeReused()
    {
        foreach (var failure in new[] { "unauthorized", "scope" })
        {
            var factory = new Factory();
            var broker = new NodeRealtimeBroker(factory, Options(), new(Options()));
            var ticket = broker.Issue("session=value", "/api/realtime/notifications/probe", Origin, Scope());
            var scope = (await broker.ConsumeAsync(ticket, Origin, default))!;
            factory.Status = failure == "unauthorized" ? HttpStatusCode.Unauthorized : HttpStatusCode.OK;
            factory.Scope = Scope() with { Room = "tenant:20:employee:12" };
            Assert.Null(await broker.ValidateAsync(scope.LeaseId, default));
            factory.Status = HttpStatusCode.OK;
            factory.Scope = Scope();
            Assert.Null(await broker.ValidateAsync(scope.LeaseId, default));
        }
    }

    [Fact]
    public async Task Lease_SqlUtcExpiryWithoutKind_IsSerializedWithExplicitUtcOffset()
    {
        var sqlExpiry = DateTime.SpecifyKind(DateTime.UtcNow.AddMinutes(1), DateTimeKind.Unspecified);
        var factory = new Factory { Scope = Scope() with { ExpiresAtUtc = sqlExpiry } };
        var broker = new NodeRealtimeBroker(factory, Options(), new(Options()));
        var ticket = broker.Issue("session=value", "/api/realtime/notifications/probe", Origin, Scope());
        var result = (await broker.ConsumeAsync(ticket, Origin, default))!;
        Assert.Equal(DateTimeKind.Utc, result.ExpiresAtUtc.Kind);
        Assert.Equal(sqlExpiry.Ticks, result.ExpiresAtUtc.Ticks);
        Assert.Contains("Z\"", System.Text.Json.JsonSerializer.Serialize(result));
    }

    [Fact]
    public async Task Ticket_WrongOriginExpiredGrantAndRestartCannotAuthenticate()
    {
        var factory = new Factory();
        var broker = new NodeRealtimeBroker(factory, Options(), new(Options()));
        var ticket = broker.Issue("session=value", "/api/realtime/notifications/probe", Origin, Scope());
        Assert.Null(await broker.ConsumeAsync(ticket, "http://evil.test", default));
        Assert.Null(await broker.ConsumeAsync(ticket, Origin, default));
        var expired = broker.Issue("session=value", "/api/realtime/notifications/probe", Origin,
            Scope() with { ExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1) });
        Assert.Null(await broker.ConsumeAsync(expired, Origin, default));
        var outstanding = broker.Issue("session=value", "/api/realtime/notifications/probe", Origin, Scope());
        var restarted = new NodeRealtimeBroker(factory, Options(), new(Options()));
        Assert.Null(await restarted.ConsumeAsync(outstanding, Origin, default));
        Assert.Empty(factory.Paths);
    }

    [Theory]
    [InlineData("https://evil.test", "http://127.0.0.1:5005", false)]
    [InlineData("http://127.0.0.1:4000", "http://localhost:5005", true)]
    [InlineData("http://127.0.0.1:4000/path", "http://localhost:5005", false)]
    public void EnabledConfiguration_RequiresLoopbackAndStrongServiceSecret(string node, string api, bool valid)
    {
        var options = new NodeRealtimeOptions { Enabled = true, BaseUrl = node, ApiBaseUrl = api, ServiceSecret = Secret };
        Assert.Equal(valid, options.IsValid());
        options.ServiceSecret = "short";
        Assert.False(options.IsValid());
        options.Enabled = false;
        Assert.True(options.IsValid());
    }

    [Fact]
    public async Task Hmac_ValidatesRawBodyAfterModelBinding_RejectsReplayTamperingAndStaleRequest()
    {
        var authentication = new RealtimeServiceAuthentication(Options());
        const string body = "{\"leaseId\":\"lease\"}";
        var request = Signed(body);
        request.Body.Position = request.Body.Length; // MVC has already deserialized it.
        Assert.True(await authentication.ValidateAsync(request));
        Assert.False(await authentication.ValidateAsync(request));
        var tampered = Signed(body);
        tampered.Body = new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        Assert.False(await authentication.ValidateAsync(tampered));
        Assert.False(await authentication.ValidateAsync(Signed(body, DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeMilliseconds())));
        var changedPath = Signed(body);
        changedPath.Path = "/internal/realtime/consume";
        Assert.False(await authentication.ValidateAsync(changedPath));
    }

    [Fact]
    public void Hmac_MatchesNodeProtocolVector()
    {
        // Independently generated with node:crypto in BackendNode/tests.
        Assert.Equal("fae7f842ba610e1b2719c643c5c350a015f98b06683bcdd73619781d68c8d7cf",
            RealtimeServiceAuthentication.Signature("key", "POST", "/internal/realtime/consume", "{}", "1000000000000", "11111111-1111-1111-1111-111111111111"));
    }

    [Fact]
    public void DeliveryMigration_IsAdditive_NoJsonOrPhysicalForeignKeys_AndSnapshotMatchesModel()
    {
        using var db = new DbDtctechContext(new DbContextOptionsBuilder<DbDtctechContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True").Options);
        Assert.False(db.Database.HasPendingModelChanges());
        var assembly = db.GetService<IMigrationsAssembly>();
        var entry = assembly.Migrations.Single(x => x.Key.EndsWith("_NodeRealtimeDelivery"));
        var migration = assembly.CreateMigration(entry.Value, db.Database.ProviderName!);
        Assert.Equal(7, migration.UpOperations.OfType<AddColumnOperation>().Count());
        Assert.Empty(migration.UpOperations.OfType<DropColumnOperation>());
        Assert.Empty(migration.UpOperations.OfType<AddForeignKeyOperation>());
        Assert.Empty(migration.UpOperations.OfType<DropTableOperation>());
        Assert.All(migration.UpOperations.OfType<AddColumnOperation>(), x => Assert.DoesNotContain("Json", x.Name));
        var row = new TblNotificationDispatchOutbox();
        Assert.Equal("Disabled", row.NodeStatus);
        row.Status = "Dispatched";
        row.NodeStatus = "Pending";
        Assert.Equal("Dispatched", row.Status);
        Assert.Null(row.NodeDispatchedAtUtc);
    }

    private static HttpRequest Signed(string body, long? timestamp = null)
    {
        var http = new DefaultHttpContext();
        http.Request.Method = "POST";
        http.Request.Path = "/internal/realtime/validate";
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        using var outgoing = new HttpRequestMessage(HttpMethod.Post, "http://localhost/internal/realtime/validate");
        new RealtimeServiceAuthentication(Options()).Sign(outgoing, "/internal/realtime/validate", body);
        foreach (var header in outgoing.Headers) http.Request.Headers[header.Key] = header.Value.ToArray();
        if (timestamp is not null)
        {
            http.Request.Headers["x-realtime-timestamp"] = timestamp.Value.ToString();
            http.Request.Headers["x-realtime-signature"] = RealtimeServiceAuthentication.Signature(Secret, "POST",
                "/internal/realtime/validate", body, timestamp.Value.ToString(), http.Request.Headers["x-realtime-nonce"]!);
        }
        return http.Request;
    }

    private sealed class Factory : HttpMessageHandler, IHttpClientFactory
    {
        public List<string> Cookies { get; } = [];
        public List<string> Paths { get; } = [];
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public NodeSubscription Scope { get; set; } = NodeRealtimeTests.Scope();
        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Cookies.Add(request.Headers.GetValues("Cookie").Single());
            Paths.Add(request.RequestUri!.AbsolutePath);
            Assert.True(request.RequestUri.IsLoopback);
            Assert.True(request.Headers.Contains("x-realtime-signature"));
            return Task.FromResult(new HttpResponseMessage(Status) { Content = JsonContent.Create(Scope) });
        }
    }

    private sealed class MemorySession : ISession
    {
        private readonly Dictionary<string, byte[]> _values = [];
        public bool IsAvailable => true;
        public string Id { get; } = Guid.NewGuid().ToString();
        public IEnumerable<string> Keys => _values.Keys;
        public void Clear() => _values.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _values.Remove(key);
        public void Set(string key, byte[] value) => _values[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _values.TryGetValue(key, out value!);
    }
}
