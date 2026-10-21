using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
namespace ContractManagement.Tests.Integration;
public sealed class HandoverSqlServerTests
{
    [HandoverSqlFact]
    [Trait("Category", "HandoverSqlServer")]
    public async Task ConcurrentActiveDraftInserts_OnlyOneWinsOnRealSqlServer()
    {
        // Explicit opt-in, isolated throwaway database only. Never uses the configured application tenant database.
        var name = "ContractManagement_Handover_Phase03_QA_" + Guid.NewGuid().ToString("N");
        var builder = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("PHASE12_SQLSERVER_CONNECTION")!) { InitialCatalog = name };
        DbDtctechContext Context() => new(new DbContextOptionsBuilder<DbDtctechContext>().UseSqlServer(builder.ConnectionString).Options);
        try
        {
            await using (var migration = Context()) await migration.Database.MigrateAsync();
            async Task<bool> Insert(string code)
            {
                await using var db = Context();
                db.TblContractHandoverRecords.Add(new() {
                    ContractId = 101, ContractVersionId = 201, HandoverBaselineId = 1, TemplateVersionId = 10,
                    HandoverCode = code, HandoverDate = new DateTime(2026,9,18), ContractSignDate = new DateTime(2026,9,12),
                    Location = "Hanoi", Status = 0, CreatedEmployeeId = 1, CreatedAt = DateTime.UtcNow });
                try { await db.SaveChangesAsync(); return true; }
                catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 }) { return false; }
            }
            var results = await Task.WhenAll(Insert("RACE-A"), Insert("RACE-B"));
            Assert.Single(results, x => x);
            await using var verification = Context(); Assert.Equal(1, await verification.TblContractHandoverRecords.CountAsync());
        }
        finally
        {
            // Resolve exact generated DB name before destructive cleanup; never target a user-supplied database.
            if (builder.InitialCatalog != name || !name.StartsWith("ContractManagement_Handover_Phase03_QA_", StringComparison.Ordinal))
                throw new InvalidOperationException("Unsafe test cleanup target.");
            await using var cleanup = Context(); await cleanup.Database.EnsureDeletedAsync();
        }
    }
    private sealed class HandoverSqlFactAttribute : FactAttribute
    {
        public HandoverSqlFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PHASE12_SQLSERVER_CONNECTION")))
                Skip = "Set PHASE12_SQLSERVER_CONNECTION to explicitly allow an isolated handover SQL concurrency test.";
        }
    }
}
