using ContractManagement.API.Domains.Email;
using ContractManagement.API.Domains.Recovery;
using ContractManagement.Infrastructure.Branding;
using ContractManagement.Infrastructure.MultiTenancy.Enums;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Central;
using ContractManagement.Infrastructure.Persistence.Recovery;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

// Explicit maintenance CLI, never hosted by the API and never runs at startup.
// Reads existing settings/secret environment variables; never prints their contents.
if (args.Length < 2)
{
    Console.WriteLine("Usage: RecoveryOps <inventory|migrate-local|check-config|smtp-test|status> <API config directory> [expected Central catalog OR SMTP recipient]");
    return 2;
}
var correlation = Guid.NewGuid().ToString("N");
try
{
    var root = Path.GetFullPath(args[1]);
    if (!File.Exists(Path.Combine(root, "appsettings.json"))) throw new OpsError("Missing API appsettings.json.");
    var config = new ConfigurationBuilder().SetBasePath(root).AddJsonFile("appsettings.json")
        .AddJsonFile("appsettings.Development.json", optional: true)
        .AddUserSecrets(typeof(AccountRecoveryOptions).Assembly, optional: true).AddEnvironmentVariables().Build();
    var settings = config.GetSection(AccountRecoveryOptions.SectionName).Get<AccountRecoveryOptions>() ?? new();
    var smtp = config.GetSection(SmtpEmailOptions.SectionName).Get<SmtpEmailOptions>() ?? new();
    if (args[0] == "check-config")
    {
        // Check the enabled configuration even when the feature remains gated off for rollout.
        settings.Enabled = true;
        if (!settings.IsValid(development: true) || (settings.Provider == "Smtp" && !smtp.IsConfigured()))
            throw new OpsError("Recovery/SMTP settings are not ready. No secret values are logged.");
        Console.WriteLine("CONFIG_READY; Development only; persistent key and trusted frontend URLs validated.");
        return 0;
    }
    if (args[0] == "smtp-test")
    {
        if (args.Length != 3 || !SmtpEmailOptions.IsEmailAddress(args[2])) throw new OpsError("Specify one explicitly approved SMTP test recipient.");
        await new SmtpEmailTransport(Options.Create(smtp)).SendAsync(new(args[2], $"Kiểm tra email khôi phục mật khẩu — {ApplicationBranding.Name}",
            $"Đây là thư thử cấu hình SMTP của hệ thống {ApplicationBranding.Name}. Không có mật khẩu, mã khôi phục hoặc thay đổi tài khoản nào trong thao tác này.",
            $"<html><body><h2>Kiểm tra email khôi phục mật khẩu</h2><p>Đây là thư thử SMTP của {ApplicationBranding.HtmlName}.</p><p><strong>Không có mật khẩu, token hoặc thay đổi tài khoản.</strong></p></body></html>"), default);
        Console.WriteLine("SMTP_ACCEPTED; explicit test recipient; no account changed. Mailbox delivery/spam placement is not asserted.");
        return 0;
    }
    var centralConnection = config.GetConnectionString("CentralDatabase");
    if (string.IsNullOrWhiteSpace(centralConnection)) throw new OpsError("Central database is not configured.");
    await using var central = new CentralDbContext(new DbContextOptionsBuilder<CentralDbContext>().UseSqlServer(centralConnection).Options);
    var tenants = await central.Tenants.AsNoTracking().Select(x => new {
        x.TenantCode, x.Status, x.TenantDatabase.Mode, x.TenantDatabase.ConnectionString
    }).ToListAsync();
    var stores = new List<Store> { new("Central", centralConnection, true) };
    foreach (var tenant in tenants)
    {
        Console.WriteLine($"TENANT {tenant.TenantCode}; {tenant.Status}; {tenant.Mode}");
        if (tenant.Status == TenantStatus.Active)
        {
            if (tenant.Mode != TenantDatabaseMode.Dedicated) throw new OpsError("Recovery rollout requires dedicated tenant databases; shared stores are not supported.");
            stores.Add(new(tenant.TenantCode, tenant.ConnectionString, false));
        }
    }
    if (stores.GroupBy(x => (x.Server.ToLowerInvariant(), x.Catalog.ToLowerInvariant())).Any(x => x.Count() > 1))
        throw new OpsError("Duplicate database routing found; rollout stopped before any writes.");
    foreach (var store in stores)
    {
        await using var db = store.Open();
        var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();
        Console.WriteLine($"STORE {store.Label}; catalog={store.Catalog}; pending={string.Join(',', pending)}");
        if (args[0] == "status" && pending.Length == 0)
        {
            var mail = db.Set<AccountRecoveryEmailOutbox>();
            var now = DateTime.UtcNow;
            Console.WriteLine($"OUTBOX {store.Label}; pending={await mail.CountAsync(x => x.Status == RecoveryDeliveryStatus.Pending)}; leased={await mail.CountAsync(x => x.Status == RecoveryDeliveryStatus.Leased)}; failed={await mail.CountAsync(x => x.Status == RecoveryDeliveryStatus.Failed)}; expired_pending={await mail.CountAsync(x => x.Status == RecoveryDeliveryStatus.Pending && x.ExpiresAtUtc < now)}");
        }
        if (args[0] == "migrate-local")
        {
            store.EnsureLocal();
            if (pending.Any(x => x != store.RecoveryMigration)) throw new OpsError($"Earlier migrations remain for {store.Label}; apply/review those separately.");
        }
    }
    if (args[0] is "inventory" or "status") return 0;
    if (args[0] != "migrate-local") throw new OpsError("Unknown operation.");
    if (args.Length != 3 || !string.Equals(args[2], stores[0].Catalog, StringComparison.Ordinal))
        throw new OpsError("Specify the exact Central catalog from inventory to authorize this local rollout.");
    // Complete all COPY_ONLY backups + VERIFYONLY before changing the first schema.
    var pendingStores = new List<Store>();
    foreach (var store in stores)
    {
        await using var db = store.Open();
        if ((await db.Database.GetPendingMigrationsAsync()).Any()) pendingStores.Add(store);
    }
    foreach (var store in pendingStores) await store.Backup(correlation);
    foreach (var store in pendingStores) // Central first, then active tenants.
    {
        await using var db = store.Open();
        await db.Database.MigrateAsync(store.RecoveryMigration);
        if ((await db.Database.GetPendingMigrationsAsync()).Any()) throw new OpsError($"Schema is still pending for {store.Label}.");
        Console.WriteLine($"MIGRATED {store.Label}; {store.RecoveryMigration}");
    }
    Console.WriteLine($"ROLLOUT_SCHEMA_READY; stores={stores.Count}; migrated={pendingStores.Count}; correlation={correlation}");
    return 0;
}
catch (OpsError ex) { Console.Error.WriteLine($"PRECHECK_FAILED: {ex.Message}; correlation={correlation}"); return 2; }
catch (Exception ex)
{
    Console.Error.WriteLine($"OPERATION_FAILED; type={ex.GetType().Name}; sql_code={(ex is SqlException sql ? sql.Number : 0)}; correlation={correlation}. No exception or secret dump.");
    return 1;
}

sealed class OpsError(string message) : Exception(message);
sealed record Store(string Label, string Connection, bool Central)
{
    private SqlConnectionStringBuilder Builder => new(Connection);
    public string Catalog => Builder.InitialCatalog;
    public string Server => Builder.DataSource;
    public string RecoveryMigration => Central ? "20261002082056_AccountRecoveryPhase123" : "20261002082219_AccountRecoveryPhase123";
    public DbContext Open() => Central
        ? new CentralDbContext(new DbContextOptionsBuilder<CentralDbContext>().UseSqlServer(Connection).Options)
        : new DbDtctechContext(new DbContextOptionsBuilder<DbDtctechContext>().UseSqlServer(Connection).Options);
    public void EnsureLocal()
    {
        var host = Server.Replace("tcp:", "", StringComparison.OrdinalIgnoreCase).Split(',', '\\')[0];
        if (!(new[] { ".", "(local)", "localhost", "127.0.0.1", "::1", "[::1]", Environment.MachineName }).Contains(host, StringComparer.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(Catalog) || (new[] { "master", "model", "msdb", "tempdb" }).Contains(Catalog, StringComparer.OrdinalIgnoreCase))
            throw new OpsError("Refusing non-local/system database migration.");
    }
    public async Task Backup(string correlation)
    {
        EnsureLocal();
        await using var connection = new SqlConnection(Connection); await connection.OpenAsync();
        await using var pathQuery = connection.CreateCommand();
        pathQuery.CommandText = "SELECT CONVERT(nvarchar(4000), SERVERPROPERTY('InstanceDefaultBackupPath'))";
        var directory = await pathQuery.ExecuteScalarAsync() as string;
        if (string.IsNullOrWhiteSpace(directory)) throw new OpsError("SQL Server default backup directory is unavailable. Configure backup before rollout.");
        var filename = $"RecoveryRollout_{correlation}_{Guid.NewGuid():N}.bak";
        var path = directory.TrimEnd('\\', '/') + (directory.Contains('\\') ? "\\" : "/") + filename;
        await using var backup = connection.CreateCommand(); backup.CommandTimeout = 300;
        backup.CommandText = "BACKUP DATABASE " + "[" + Catalog.Replace("]", "]]") + "] TO DISK = @path WITH COPY_ONLY, CHECKSUM; RESTORE VERIFYONLY FROM DISK = @path WITH CHECKSUM;";
        backup.Parameters.AddWithValue("@path", path);
        await backup.ExecuteNonQueryAsync();
        Console.WriteLine($"BACKUP_VERIFIED {Label}; path={path}");
    }
}
