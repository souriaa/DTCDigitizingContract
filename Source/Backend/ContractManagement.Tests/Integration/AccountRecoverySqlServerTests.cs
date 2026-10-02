using System.Security.Cryptography;
using ContractManagement.API.Common.Security;
using ContractManagement.API.Domains.Recovery;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using ContractManagement.Infrastructure.Persistence.Central;
using ContractManagement.Infrastructure.Persistence.Central.Entities;
using ContractManagement.Infrastructure.Persistence.Recovery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ContractManagement.Tests.Integration;

public sealed class RecoverySqlTheoryAttribute : TheoryAttribute
{
    public RecoverySqlTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RECOVERY_SQLSERVER_CONNECTION")))
            Skip = "Set RECOVERY_SQLSERVER_CONNECTION to a server permitting isolated Recovery_QA_ databases.";
    }
}

public sealed class AccountRecoverySqlServerTests
{
    [RecoverySqlTheory, InlineData(true, false), InlineData(false, false), InlineData(true, true), InlineData(false, true)]
    [Trait("Category", "RecoverySqlServer")]
    public async Task FreshAndSeededUpgrade_ConstraintsAndAtomicVerification(bool central, bool upgrade)
    {
        var name = "Recovery_QA_" + Guid.NewGuid().ToString("N");
        var builder = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("RECOVERY_SQLSERVER_CONNECTION")) {
            InitialCatalog = name, Pooling = false
        };
        DbContext Context() => central ? new CentralDbContext(new DbContextOptionsBuilder<CentralDbContext>().UseSqlServer(builder.ConnectionString).Options)
            : new DbDtctechContext(new DbContextOptionsBuilder<DbDtctechContext>().UseSqlServer(builder.ConnectionString).Options);
        await using var db = Context();
        try
        {
            var migrations = db.Database.GetMigrations().ToArray();
            Assert.EndsWith("_AccountRecoveryPhase123", migrations[^1]);
            if (upgrade)
            {
                await db.Database.MigrateAsync(migrations[^2]);
                await db.Database.ExecuteSqlRawAsync(central ? """
                    INSERT INTO SystemAdmins (Username, PasswordHash, FullName, Email, IsActive, CreatedAt)
                    VALUES ('qa-baseline', 'qa-placeholder', N'QA Baseline', 'baseline@example.test', 1, SYSUTCDATETIME())
                    """ : """
                    INSERT INTO tbl_Employee (EmployeeAccount, EmployeePassword, EmployeeFullName, EmployeeEmail, Status, DateCreated)
                    VALUES ('qa-baseline', 'qa-placeholder', N'QA Baseline', 'baseline@example.test', 1, SYSUTCDATETIME())
                    """);
            }
            await db.Database.MigrateAsync();
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            if (upgrade)
            {
                Assert.Null(central ? (await ((CentralDbContext)db).SystemAdmins.SingleAsync()).RecoveryEmailVerifiedAt
                    : (await ((DbDtctechContext)db).TblEmployees.SingleAsync()).RecoveryEmailVerifiedAt);
            }
            await using (var connection = new SqlConnection(builder.ConnectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM sys.foreign_keys WHERE OBJECT_NAME(parent_object_id) LIKE '%AccountRecovery%'";
                Assert.Equal(0, Convert.ToInt32(await command.ExecuteScalarAsync()));
            }
            using var services = new ServiceCollection().AddSingleton<IPasswordHasher<SystemAdmin>, PasswordHasher<SystemAdmin>>()
                .AddSingleton<IPasswordHasher<TblEmployee>, PasswordHasher<TblEmployee>>().BuildServiceProvider();
            var settings = new AccountRecoveryOptions { Enabled = true, Provider = "Test",
                EncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                TenantFrontendBaseUrl = "http://localhost:3000", SystemAdminFrontendBaseUrl = "http://localhost:3001" };
            var crypto = new AccountRecoveryCryptography(Options.Create(settings));
            int id;
            if (central)
            {
                var admin = new SystemAdmin { Username = "qa-active", FullName = "QA", IsActive = true, Email = "qa@example.test", SessionVersion = 1, CreatedAt = DateTime.UtcNow };
                admin.PasswordHash = services.GetRequiredService<IPasswordHasher<SystemAdmin>>().HashPassword(admin, "Password123!");
                db.Add(admin); await db.SaveChangesAsync(); id = admin.SystemAdminId;
            }
            else
            {
                var employee = new TblEmployee { EmployeeAccount = "qa-active", EmployeeFullName = "QA", EmployeeEmail = "qa@example.test", Status = 1, SessionVersion = 1, DateCreated = DateTime.UtcNow };
                employee.EmployeePassword = services.GetRequiredService<IPasswordHasher<TblEmployee>>().HashPassword(employee, "Password123!");
                db.Add(employee); await db.SaveChangesAsync(); id = employee.EmployeeId;
            }
            RecoveryStore Store(DbContext context) => new(context, central ? "central" : "tenant:1", central ? null : 1, central ? null : "qa");
            var service = new RecoveryEmailVerificationService(services, Options.Create(settings), crypto, TimeProvider.System, new HttpContextAccessor());
            await service.RequestAsync(Store(db), id, "Password123!", default);
            var challenge = await db.Set<AccountRecoveryChallenge>().SingleAsync();
            Assert.Equal(8, challenge.RowVersion.Length);
            var token = crypto.Decrypt((await db.Set<AccountRecoveryEmailOutbox>().SingleAsync()).TokenCiphertext!, Store(db).Scope);
            // Unique open challenge and pending delivery constraints are real SQL indexes.
            db.Add(new AccountRecoveryChallenge { AccountId = id, PublicId = Guid.NewGuid(), Purpose = RecoveryPurpose.VerifyRecoveryEmail,
                TokenHash = new byte[32], EmailHash = new byte[32], SessionVersionAtIssue = 1, CreatedAtUtc = DateTime.UtcNow,
                LastRequestedAtUtc = DateTime.UtcNow, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10), RequestCount = 1 });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
            db.Add(new AccountRecoveryEmailOutbox { AccountId = id, RecoveryChallengeId = challenge.RecoveryChallengeId,
                TemplateKind = RecoveryEmailKind.VerifyRecoveryEmail, Status = RecoveryDeliveryStatus.Pending, CreatedAtUtc = DateTime.UtcNow, NextAttemptAtUtc = DateTime.UtcNow });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();

            await using var other = Context();
            // Both requests have read the same rowversions before either writes.
            await Store(db).LoadAccountAsync(id, default); await Store(other).LoadAccountAsync(id, default);
            await db.Set<AccountRecoveryChallenge>().SingleAsync(); await other.Set<AccountRecoveryChallenge>().SingleAsync();
            async Task<string> Verify(DbContext context)
            {
                try { await service.VerifyAsync(Store(context), id, challenge.PublicId, token, default); return "success"; }
                catch (RbacOperationException ex) { return ex.Code; }
            }
            var outcomes = await Task.WhenAll(Verify(db), Verify(other));
            Assert.Single(outcomes, x => x == "success");
            Assert.Contains(outcomes, x => x == "RecoveryEmailConflict" || x == "RecoveryEmailLinkInvalid");
            db.ChangeTracker.Clear();
            Assert.NotNull((await db.Set<AccountRecoveryChallenge>().SingleAsync()).UsedAtUtc);
            Assert.Null((await db.Set<AccountRecoveryEmailOutbox>().SingleAsync()).TokenCiphertext);
            Assert.NotNull((await Store(db).LoadAccountAsync(id, default))!.VerifiedAt);
            Assert.Equal(2, central ? await ((CentralDbContext)db).SecurityAudits.CountAsync()
                : await ((DbDtctechContext)db).TblAuthorizationAudits.CountAsync());
            // A public reset uses the same SQL transaction/rowversions, with a single winner.
            var passwordService = new PasswordRecoveryService(services, Options.Create(settings), crypto, TimeProvider.System, new HttpContextAccessor());
            await passwordService.RequestAsync(Store(db), "qa-active", default);
            var resetChallenge = await db.Set<AccountRecoveryChallenge>().SingleAsync(x => x.Purpose == RecoveryPurpose.PasswordReset);
            var resetToken = crypto.Decrypt((await db.Set<AccountRecoveryEmailOutbox>().SingleAsync(x => x.TemplateKind == RecoveryEmailKind.PasswordReset)).TokenCiphertext!, Store(db).Scope);
            other.ChangeTracker.Clear();
            await Store(other).LoadAccountAsync(id, default);
            await other.Set<AccountRecoveryChallenge>().SingleAsync(x => x.Purpose == RecoveryPurpose.PasswordReset);
            async Task<string> Reset(DbContext context)
            {
                try { await passwordService.ResetAsync(Store(context), resetChallenge.PublicId, resetToken, "ResetPassword123!", "ResetPassword123!", default); return "success"; }
                catch (RbacOperationException ex) { return ex.Code; }
            }
            outcomes = await Task.WhenAll(Reset(db), Reset(other));
            Assert.Single(outcomes, x => x == "success");
            Assert.Contains(outcomes, x => x == "PasswordRecoveryConflict" || x == "PasswordRecoveryLinkInvalid");
            db.ChangeTracker.Clear();
            var resetAccount = (await Store(db).LoadAccountAsync(id, default))!;
            Assert.Equal(2, resetAccount.SessionVersion);
            resetAccount.VerifyPassword(services, "ResetPassword123!");
            Assert.NotNull((await db.Set<AccountRecoveryChallenge>().SingleAsync(x => x.Purpose == RecoveryPurpose.PasswordReset)).UsedAtUtc);
            var notice = await db.Set<AccountRecoveryEmailOutbox>().SingleAsync(x => x.TemplateKind == RecoveryEmailKind.PasswordChanged);
            Assert.Null(notice.TokenCiphertext); Assert.Null(notice.RecoveryChallengeId);
            Assert.Equal(4, central ? await ((CentralDbContext)db).SecurityAudits.CountAsync()
                : await ((DbDtctechContext)db).TblAuthorizationAudits.CountAsync());
            Assert.Equal("PasswordRecoveryLinkInvalid", (await Assert.ThrowsAsync<RbacOperationException>(() => passwordService.ResetAsync(Store(db), resetChallenge.PublicId, resetToken, "AnotherPassword1!", "AnotherPassword1!", default))).Code);
        }
        finally
        {
            // Never deletes the configured database: only the exact random QA target above.
            Assert.Matches("^Recovery_QA_[a-f0-9]{32}$", builder.InitialCatalog);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
