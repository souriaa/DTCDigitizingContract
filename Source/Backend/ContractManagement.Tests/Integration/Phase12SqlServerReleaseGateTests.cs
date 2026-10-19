using ContractManagement.API.Common.Enums;
using ContractManagement.API.Common.Security;
using ContractManagement.API.Domains.DTOs.Requests.Contract;
using ContractManagement.Common.Enums;
using ContractManagement.Domains.Interfaces.Contract;
using ContractManagement.Domains.Services.Contract;
using ContractManagement.Infrastructure.MultiTenancy.Enums;
using ContractManagement.Infrastructure.MultiTenancy.Models;
using ContractManagement.Infrastructure.MultiTenancy.Services;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Tests.Integration;

public sealed class Phase12SqlServerReleaseGateTests
{
    private const string ConnectionVariable =
        "PHASE12_SQLSERVER_CONNECTION";

    [Phase12SqlServerFact]
    [Trait("Category", "Phase12SqlServer")]
    public async Task RealSqlServer_MigratesTwoTenantDatabases_AndEnforcesIsolationRbacAndRowVersion()
    {
        var baseConnection = Environment.GetEnvironmentVariable(
            ConnectionVariable)!;
        var runId = Guid.NewGuid().ToString("N");
        var databaseA = $"ContractManagement_Phase12_QA_A_{runId}";
        var databaseB = $"ContractManagement_Phase12_QA_B_{runId}";
        var connectionA = WithDatabase(baseConnection, databaseA);
        var connectionB = WithDatabase(baseConnection, databaseB);

        try
        {
            await using (var tenantA = CreateContext(connectionA))
            await using (var tenantB = CreateContext(connectionB))
            {
                await tenantA.Database.MigrateAsync();
                await tenantB.Database.MigrateAsync();

                Assert.Empty(await tenantA.Database.GetPendingMigrationsAsync());
                Assert.Empty(await tenantB.Database.GetPendingMigrationsAsync());
            }

            await AssertNormalizedPersistenceSchemaAsync(connectionA);
            await AssertNormalizedPersistenceSchemaAsync(connectionB);

            var seeded = await SeedTenantAAsync(connectionA);
            var tenantBEmployeeId = await SeedTenantBEmployeeAsync(connectionB);

            await using (var tenantA = CreateContext(connectionA))
            {
                var authorization = new ContractResourceAuthorizationService(
                    tenantA);

                await authorization.EnsureCanReadAsync(
                    seeded.ContractId,
                    seeded.OwnerId);
                await authorization.EnsureCanWriteAsync(
                    seeded.ContractId,
                    seeded.OwnerId);
                await authorization.EnsureCanReadAsync(
                    seeded.ContractId,
                    seeded.ManagerId);

                var managerWrite = await Assert.ThrowsAsync<RbacOperationException>(
                    () => authorization.EnsureCanWriteAsync(
                        seeded.ContractId,
                        seeded.ManagerId));
                var unrelatedRead = await Assert.ThrowsAsync<RbacOperationException>(
                    () => authorization.EnsureCanReadAsync(
                        seeded.ContractId,
                        seeded.UnrelatedEmployeeId));

                Assert.Equal(
                    AuthorizationErrorCodes.ResourceNotFound,
                    managerWrite.Code);
                Assert.Equal(
                    AuthorizationErrorCodes.ResourceNotFound,
                    unrelatedRead.Code);
            }

            await using (var tenantB = CreateContext(connectionB))
            {
                var crossTenantRead = await Assert.ThrowsAsync<RbacOperationException>(
                    () => new ContractResourceAuthorizationService(tenantB)
                        .EnsureCanReadAsync(
                            seeded.ContractId,
                            tenantBEmployeeId));

                Assert.Equal(
                    AuthorizationErrorCodes.ResourceNotFound,
                    crossTenantRead.Code);
                Assert.Empty(tenantB.TblContracts);
            }

            await AssertSqlServerRowVersionAsync(
                connectionA,
                seeded.ContractId);
        }
        finally
        {
            await DropQaDatabaseAsync(connectionA, databaseA);
            await DropQaDatabaseAsync(connectionB, databaseB);
        }
    }

    [Phase12SqlServerFact]
    [Trait("Category", "Phase12SqlServer")]
    public async Task AddAppendix_WhenSecondStageFails_RollsBackParentAndTerms()
    {
        var baseConnection = Environment.GetEnvironmentVariable(
            ConnectionVariable)!;
        var databaseName =
            $"ContractManagement_Phase12_QA_Appendix_{Guid.NewGuid():N}";
        var connectionString = WithDatabase(baseConnection, databaseName);

        try
        {
            int contractId;
            int versionId;
            int templateAppendixId;
            int employeeId;
            string versionRowVersion;
            await using (var seed = CreateContext(connectionString))
            {
                await seed.Database.MigrateAsync();
                var employee = Employee(
                    "PH12-APPENDIX-OWNER", EmployeeType.Sale);
                seed.TblEmployees.Add(employee);
                await seed.SaveChangesAsync();

                var customer = new TblCustomer
                {
                    CustomerCode = "PH12-APPENDIX-CUSTOMER",
                    CustomerFullName = "Phase 12 Appendix Customer",
                    Status = 1
                };
                var template = new TblContractTemplate
                {
                    TemplateCode = "PH12-APPENDIX-TEMPLATE",
                    TemplateName = "Phase 12 Appendix Template",
                    DocumentType =
                        (byte)TemplateDocumentType.SoftwareSupplyContract,
                    LanguageMode =
                        (byte)ContractLanguageMode.Vietnamese,
                    IsActive = true,
                    CreatedEmployeeId = employee.EmployeeId,
                    CreatedDate = DateTime.UtcNow
                };
                seed.TblCustomers.Add(customer);
                seed.TblContractTemplates.Add(template);
                await seed.SaveChangesAsync();

                var templateVersion = new TblContractTemplateVersion
                {
            ContractDirection = (byte)1,
                    TemplateId = template.TemplateId,
                    VersionNo = 1,
                    Status = (byte)TemplateVersionStatus.Draft,
                    ValidationStatus =
                        (byte)TemplateValidationStatus.NotValidated,
                    CreatedEmployeeId = employee.EmployeeId,
                    CreatedDate = DateTime.UtcNow
                };
                seed.TblContractTemplateVersions.Add(templateVersion);
                await seed.SaveChangesAsync();

                var sourceAppendix = new TblContractTemplateAppendix
                {
                    TemplateVersionId = templateVersion.TemplateVersionId,
                    AppendixCode = "PL-ROLLBACK",
                    AppendixName = "Phụ lục kiểm tra rollback",
                    IsRequired = false,
                    IsSelectedByDefault = false,
                    DisplayOrder = 1,
                    CreatedEmployeeId = employee.EmployeeId,
                    CreatedDate = DateTime.UtcNow
                };
                seed.TblContractTemplateAppendices.Add(sourceAppendix);
                await seed.SaveChangesAsync();
                seed.TblContractTemplateAppendixTerms.Add(
                    new TblContractTemplateAppendixTerm
                    {
                        TemplateAppendixId =
                            sourceAppendix.TemplateAppendixId,
                        TermCode = "PL-RB-01",
                        TermTitle = "Điều khoản rollback",
                        DisplayOrder = 1,
                        CreatedEmployeeId = employee.EmployeeId,
                        CreatedDate = DateTime.UtcNow
                    });

                var contract = new TblContract
                {
            ContractDirection = (byte)1,
                    CustomerId = customer.CustomerId,
                    EmployeeId = employee.EmployeeId,
                    ContractType = (byte)ContractType.SoftwareSupply,
                    TemplateVersionId = templateVersion.TemplateVersionId,
                    ContractCode = "PH12-APPENDIX-ROLLBACK",
                    ContractName = "Phase 12 Appendix Rollback",
                    Status = (byte)ContractStatus.Draft,
                    CurrencyCode = "VND",
                    LanguageMode =
                        (byte)ContractLanguageMode.Vietnamese,
                    CreatedEmployeeId = employee.EmployeeId,
                    CreatedDate = DateTime.UtcNow
                };
                seed.TblContracts.Add(contract);
                await seed.SaveChangesAsync();
                var version = new TblContractVersion
                {
                    ContractId = contract.ContractId,
                    VersionNo = 1,
                    TemplateVersionId = templateVersion.TemplateVersionId,
                    CurrencyCode = "VND",
                    IsLocked = false,
                    CreatedEmployeeId = employee.EmployeeId,
                    CreatedDate = DateTime.UtcNow
                };
                seed.TblContractVersions.Add(version);
                await seed.SaveChangesAsync();
                contract.CurrentVersionId = version.VersionId;
                await seed.SaveChangesAsync();

                contractId = contract.ContractId;
                versionId = version.VersionId;
                templateAppendixId = sourceAppendix.TemplateAppendixId;
                employeeId = employee.EmployeeId;
                versionRowVersion = Convert.ToBase64String(
                    version.RowVersion);
            }

            await using (var action = CreateContext(connectionString))
            {
                var service = new ContractService(
                    action,
                    new ThrowingAuditWriter());
                var failure = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => service.AddAppendixAsync(
                        contractId,
                        versionId,
                        new AddContractAppendixRequest
                        {
                            TemplateAppendixId = templateAppendixId,
                            VersionRowVersion = versionRowVersion
                        },
                        employeeId));
                Assert.Equal("Simulated audit staging failure.", failure.Message);
            }

            await using var verify = CreateContext(connectionString);
            Assert.False(await verify.TblContractAppendices.AnyAsync(x =>
                x.ContractId == contractId && x.VersionId == versionId));
            Assert.Empty(await verify.TblContractAppendixTerms.ToListAsync());
        }
        finally
        {
            await DropQaDatabaseAsync(connectionString, databaseName);
        }
    }

    [Phase12SqlServerFact]
    [Trait("Category", "Phase12SqlServer")]
    public async Task DraftAppendix_AddTermAndSwapOrder_SucceedsOnSqlServer()
    {
        var baseConnection = Environment.GetEnvironmentVariable(
            ConnectionVariable)!;
        var databaseName =
            $"ContractManagement_Phase12_QA_AppendixTerms_{Guid.NewGuid():N}";
        var connectionString = WithDatabase(baseConnection, databaseName);

        try
        {
            await using var context = CreateContext(connectionString);
            await context.Database.MigrateAsync();

            var employee = Employee(
                "PH12-APPENDIX-TERM-OWNER", EmployeeType.Sale);
            var customer = new TblCustomer
            {
                CustomerCode = "PH12-APPENDIX-TERM-CUSTOMER",
                CustomerFullName = "Phase 12 Appendix Term Customer",
                Status = 1
            };
            var template = new TblContractTemplate
            {
                TemplateCode = "PH12-APPENDIX-TERM-TEMPLATE",
                TemplateName = "Phase 12 Appendix Term Template",
                DocumentType =
                    (byte)TemplateDocumentType.SoftwareSupplyContract,
                LanguageMode = (byte)ContractLanguageMode.Vietnamese,
                IsActive = true,
                CreatedEmployeeId = 0,
                CreatedDate = DateTime.UtcNow
            };
            context.TblEmployees.Add(employee);
            context.TblCustomers.Add(customer);
            await context.SaveChangesAsync();
            template.CreatedEmployeeId = employee.EmployeeId;
            context.TblContractTemplates.Add(template);
            await context.SaveChangesAsync();

            var templateVersion = new TblContractTemplateVersion
            {
            ContractDirection = (byte)1,
                TemplateId = template.TemplateId,
                VersionNo = 1,
                Status = (byte)TemplateVersionStatus.Draft,
                ValidationStatus =
                    (byte)TemplateValidationStatus.NotValidated,
                CreatedEmployeeId = employee.EmployeeId,
                CreatedDate = DateTime.UtcNow
            };
            context.TblContractTemplateVersions.Add(templateVersion);
            await context.SaveChangesAsync();

            var sourceAppendix = new TblContractTemplateAppendix
            {
                TemplateVersionId = templateVersion.TemplateVersionId,
                AppendixCode = "PL-SQL",
                AppendixName = "Phụ lục SQL",
                IsRequired = false,
                IsSelectedByDefault = false,
                DisplayOrder = 1,
                CreatedEmployeeId = employee.EmployeeId,
                CreatedDate = DateTime.UtcNow
            };
            var secondSourceAppendix = new TblContractTemplateAppendix
            {
                TemplateVersionId = templateVersion.TemplateVersionId,
                AppendixCode = "PL-SQL-02",
                AppendixName = "Phụ lục SQL hai",
                IsRequired = false,
                IsSelectedByDefault = false,
                DisplayOrder = 2,
                CreatedEmployeeId = employee.EmployeeId,
                CreatedDate = DateTime.UtcNow
            };
            context.TblContractTemplateAppendices.AddRange(
                sourceAppendix, secondSourceAppendix);
            await context.SaveChangesAsync();

            var contract = new TblContract
            {
            ContractDirection = (byte)1,
                CustomerId = customer.CustomerId,
                EmployeeId = employee.EmployeeId,
                ContractType = (byte)ContractType.SoftwareSupply,
                TemplateVersionId = templateVersion.TemplateVersionId,
                ContractCode = "PH12-APPENDIX-TERM-CONTRACT",
                ContractName = "Phase 12 Appendix Term Contract",
                Status = (byte)ContractStatus.Draft,
                CurrencyCode = "VND",
                LanguageMode = (byte)ContractLanguageMode.Vietnamese,
                CreatedEmployeeId = employee.EmployeeId,
                CreatedDate = DateTime.UtcNow
            };
            context.TblContracts.Add(contract);
            await context.SaveChangesAsync();

            var version = new TblContractVersion
            {
                ContractId = contract.ContractId,
                VersionNo = 1,
                TemplateVersionId = templateVersion.TemplateVersionId,
                CurrencyCode = "VND",
                IsLocked = false,
                CreatedEmployeeId = employee.EmployeeId,
                CreatedDate = DateTime.UtcNow
            };
            context.TblContractVersions.Add(version);
            await context.SaveChangesAsync();
            contract.CurrentVersionId = version.VersionId;

            var appendix = new TblContractAppendix
            {
                ContractId = contract.ContractId,
                VersionId = version.VersionId,
                SourceTemplateAppendixId = sourceAppendix.TemplateAppendixId,
                AppendixCode = "PL-SQL",
                AppendixName = "Phụ lục SQL",
                IsRequired = false,
                DisplayOrder = 1,
                CreatedEmployeeId = employee.EmployeeId,
                CreatedDate = DateTime.UtcNow
            };
            var secondAppendix = new TblContractAppendix
            {
                ContractId = contract.ContractId,
                VersionId = version.VersionId,
                SourceTemplateAppendixId =
                    secondSourceAppendix.TemplateAppendixId,
                AppendixCode = "PL-SQL-02",
                AppendixName = "Phụ lục SQL hai",
                IsRequired = false,
                DisplayOrder = 2,
                CreatedEmployeeId = employee.EmployeeId,
                CreatedDate = DateTime.UtcNow
            };
            context.TblContractAppendices.AddRange(appendix, secondAppendix);
            await context.SaveChangesAsync();

            var first = new TblContractAppendixTerm
            {
                AppendixId = appendix.AppendixId,
                TermCode = "TERM-1",
                TermTitle = "Điều một",
                DisplayOrder = 1,
                CreatedEmployeeId = employee.EmployeeId,
                CreatedDate = DateTime.UtcNow
            };
            var second = new TblContractAppendixTerm
            {
                AppendixId = appendix.AppendixId,
                TermCode = "TERM-2",
                TermTitle = "Điều hai",
                DisplayOrder = 2,
                CreatedEmployeeId = employee.EmployeeId,
                CreatedDate = DateTime.UtcNow
            };
            context.TblContractAppendixTerms.AddRange(first, second);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            var service = new ContractService(
                context,
                CreateAuditWriter(context));
            var currentVersion = await context.TblContractVersions
                .AsNoTracking().SingleAsync(x => x.VersionId == version.VersionId);
            var currentAppendix = await context.TblContractAppendices
                .AsNoTracking().SingleAsync(x => x.AppendixId == appendix.AppendixId);
            var added = await service.AddAppendixTermAsync(
                contract.ContractId,
                version.VersionId,
                appendix.AppendixId,
                new CreateContractAppendixTermRequest
                {
                    TermCode = "TERM-3",
                    TermTitle = "Điều ba",
                    DisplayOrder = 3,
                    VersionRowVersion = Convert.ToBase64String(
                        currentVersion.RowVersion),
                    AppendixRowVersion = Convert.ToBase64String(
                        currentAppendix.RowVersion)
                },
                employee.EmployeeId);

            context.ChangeTracker.Clear();
            currentVersion = await context.TblContractVersions
                .AsNoTracking().SingleAsync(x => x.VersionId == version.VersionId);
            currentAppendix = await context.TblContractAppendices
                .AsNoTracking().SingleAsync(x => x.AppendixId == appendix.AppendixId);
            var currentTerms = await context.TblContractAppendixTerms
                .AsNoTracking().Where(x => x.AppendixId == appendix.AppendixId)
                .ToDictionaryAsync(x => x.AppendixTermId);

            var reordered = await service.ReorderAppendixTermsAsync(
                contract.ContractId,
                version.VersionId,
                appendix.AppendixId,
                new ReorderContractAppendixTermsRequest
                {
                    VersionRowVersion = Convert.ToBase64String(
                        currentVersion.RowVersion),
                    AppendixRowVersion = Convert.ToBase64String(
                        currentAppendix.RowVersion),
                    Terms =
                    [
                        Reorder(second.AppendixTermId, 1),
                        Reorder(first.AppendixTermId, 2),
                        Reorder(added.AppendixTermId, 3)
                    ]
                },
                employee.EmployeeId);

            Assert.Equal(
                [second.AppendixTermId, first.AppendixTermId,
                    added.AppendixTermId],
                reordered.Terms.Select(item => item.AppendixTermId));
            Assert.Equal(2, await context.TblContractAudits.CountAsync(audit =>
                audit.SubjectType == ContractAuditSubjectTypes.ContractAppendix
                && audit.SubjectId == appendix.AppendixId));

            context.ChangeTracker.Clear();
            currentVersion = await context.TblContractVersions
                .AsNoTracking().SingleAsync(x => x.VersionId == version.VersionId);
            var currentAppendices = await context.TblContractAppendices
                .AsNoTracking().Where(x => x.VersionId == version.VersionId)
                .ToDictionaryAsync(x => x.AppendixId);
            var reorderedAppendices = await service.ReorderAppendicesAsync(
                contract.ContractId,
                version.VersionId,
                new ReorderContractAppendicesRequest
                {
                    VersionRowVersion = Convert.ToBase64String(
                        currentVersion.RowVersion),
                    Appendices =
                    [
                        ReorderAppendix(secondAppendix.AppendixId, 1),
                        ReorderAppendix(appendix.AppendixId, 2)
                    ]
                },
                employee.EmployeeId);
            Assert.Equal(
                [secondAppendix.AppendixId, appendix.AppendixId],
                reorderedAppendices.Select(item => item.AppendixId));
            Assert.Contains(await context.TblContractAudits.AsNoTracking()
                    .ToListAsync(),
                audit => audit.ActionType ==
                    ContractAuditActionTypes.ContractAppendicesReordered
                    && audit.SubjectType ==
                    ContractAuditSubjectTypes.ContractVersion
                    && audit.SubjectId == version.VersionId);

            context.ChangeTracker.Clear();
            currentVersion = await context.TblContractVersions
                .AsNoTracking().SingleAsync(x => x.VersionId == version.VersionId);
            var customAppendix = await service.CreateAppendixAsync(
                contract.ContractId,
                version.VersionId,
                new CreateContractAppendixRequest
                {
                    AppendixCode = "PL-CUSTOM",
                    AppendixName = "Phụ lục tạo trực tiếp",
                    VersionRowVersion = Convert.ToBase64String(
                        currentVersion.RowVersion)
                },
                employee.EmployeeId);
            Assert.Null(customAppendix.SourceTemplateAppendixId);
            Assert.Empty(customAppendix.Terms);

            ReorderContractAppendixTermItem Reorder(int termId, int order) =>
                new()
                {
                    AppendixTermId = termId,
                    DisplayOrder = order,
                    RowVersion = Convert.ToBase64String(
                        currentTerms[termId].RowVersion)
                };

            ReorderContractAppendixItem ReorderAppendix(
                int appendixId, int order) => new()
            {
                AppendixId = appendixId,
                DisplayOrder = order,
                RowVersion = Convert.ToBase64String(
                    currentAppendices[appendixId].RowVersion)
            };
        }
        finally
        {
            await DropQaDatabaseAsync(connectionString, databaseName);
        }
    }

    private static async Task<SeededTenantA> SeedTenantAAsync(
        string connectionString)
    {
        await using var context = CreateContext(connectionString);
        var owner = Employee("PH12-OWNER", EmployeeType.Sale);
        var manager = Employee("PH12-MANAGER", EmployeeType.Manager);
        var unrelated = Employee("PH12-OTHER", EmployeeType.Marketing);
        context.TblEmployees.AddRange(owner, manager, unrelated);
        await context.SaveChangesAsync();

        var customer = new TblCustomer
        {
            CustomerCode = "PH12-CUSTOMER",
            CustomerFullName = "Phase 12 Customer",
            Status = 1
        };
        context.TblCustomers.Add(customer);
        await context.SaveChangesAsync();

        var contract = new TblContract
        {
            ContractDirection = (byte)1,
            CustomerId = customer.CustomerId,
            EmployeeId = owner.EmployeeId,
            ContractType = (byte)ContractType.SoftwareSupply,
            ContractCode = $"PH12-{Guid.NewGuid():N}",
            ContractName = "Phase 12 SQL Server isolation contract",
            TemplateVersionId = 1,
            Status = (byte)ContractStatus.Draft,
            TotalAmount = 100m,
            Subtotal = 100m,
            CurrencyCode = "VND",
            LanguageMode = (byte)ContractLanguageMode.Vietnamese,
            CreatedEmployeeId = owner.EmployeeId,
            CreatedDate = DateTime.UtcNow
        };
        context.TblContracts.Add(contract);
        await context.SaveChangesAsync();

        Assert.NotEmpty(contract.RowVersion);
        return new SeededTenantA(
            contract.ContractId,
            owner.EmployeeId,
            manager.EmployeeId,
            unrelated.EmployeeId);
    }

    private static async Task<int> SeedTenantBEmployeeAsync(
        string connectionString)
    {
        await using var context = CreateContext(connectionString);
        var employee = Employee("PH12-TENANT-B", EmployeeType.Manager);
        context.TblEmployees.Add(employee);
        await context.SaveChangesAsync();
        return employee.EmployeeId;
    }

    private static async Task AssertSqlServerRowVersionAsync(
        string connectionString,
        int contractId)
    {
        await using var firstContext = CreateContext(connectionString);
        await using var staleContext = CreateContext(connectionString);
        var first = await firstContext.TblContracts.SingleAsync(contract =>
            contract.ContractId == contractId);
        var stale = await staleContext.TblContracts.SingleAsync(contract =>
            contract.ContractId == contractId);
        var initialVersion = first.RowVersion.ToArray();

        first.ContractName = "Phase 12 first concurrent update";
        await firstContext.SaveChangesAsync();

        Assert.False(initialVersion.AsSpan().SequenceEqual(first.RowVersion));
        stale.ContractName = "Phase 12 stale concurrent update";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            staleContext.SaveChangesAsync());
    }

    private static async Task AssertNormalizedPersistenceSchemaAsync(
        string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var removedColumns = connection.CreateCommand();
        removedColumns.CommandText =
            """
            SELECT COUNT_BIG(*)
            FROM sys.columns AS c
            INNER JOIN sys.tables AS t ON t.object_id = c.object_id
            WHERE (t.name = 'tbl_ContractAudit'
                    AND c.name IN ('PreviousValuesJson', 'NewValuesJson'))
               OR (t.name = 'tbl_ContractTemplateAudit'
                    AND c.name IN ('PreviousValuesJson', 'NewValuesJson'))
               OR (t.name = 'tbl_ContractPlaceholderAudit'
                    AND c.name IN ('PreviousValuesJson', 'NewValuesJson'))
               OR (t.name = 'tbl_ContractVersion' AND c.name = 'SnapshotJson')
               OR (t.name = 'tbl_ContractCustomerOtpDeliveryOutbox'
                    AND c.name = 'EncryptedPayload');
            """;
        Assert.Equal(0L, (long)(await removedColumns.ExecuteScalarAsync())!);

        await using var newSchema = connection.CreateCommand();
        newSchema.CommandText =
            """
            SELECT
                (SELECT COUNT_BIG(*)
                 FROM sys.tables
                 WHERE name IN (
                    'tbl_ContractAuditValue',
                    'tbl_ContractTemplateAuditValue',
                    'tbl_ContractVersionLegalSnapshot',
                    'tbl_ContractVersionPartySnapshot',
                    'tbl_ContractVersionPaymentMilestoneSnapshot'))
              + (SELECT COUNT_BIG(*)
                 FROM sys.columns AS c
                 INNER JOIN sys.tables AS t ON t.object_id = c.object_id
                 WHERE t.name = 'tbl_ContractCustomerOtpDeliveryOutbox'
                   AND c.name IN (
                    'PhoneCiphertext', 'OtpCiphertext',
                    'EmailCiphertext', 'DeliveryExpiresAt'));
            """;
        Assert.Equal(9L, (long)(await newSchema.ExecuteScalarAsync())!);

        await using var schemaGuards = connection.CreateCommand();
        schemaGuards.CommandText =
            """
            SELECT
                (SELECT COUNT_BIG(*)
                 FROM sys.check_constraints
                 WHERE name IN (
                    'CK_tbl_ContractVersion_LockState',
                    'CK_tbl_ContractCustomerOtpDeliveryOutbox_Ciphertexts',
                    'CK_tbl_ContractCustomerOtpDeliveryOutbox_Expiry',
                    'CK_tbl_ContractVersionLegalSnapshot_Financials',
                    'CK_tbl_ContractVersionLegalSnapshot_Ids',
                    'CK_tbl_ContractVersionPartySnapshot_Role',
                    'CK_tbl_ContractVersionPartySnapshot_Source',
                    'CK_tbl_ContractVersionPaymentMilestoneSnapshot_Ids',
                    'CK_tbl_ContractVersionPaymentMilestoneSnapshot_Values'))
              + (SELECT COUNT_BIG(*)
                 FROM sys.indexes
                 WHERE name IN (
                    'IX_tbl_ContractVersionLegalSnapshot_VersionId',
                    'IX_tbl_ContractVersionPartySnapshot_ContractVersionLegalSnapshotId_PartyRole',
                    'IX_tbl_ContractVersionPaymentMilestoneSnapshot_ContractVersionLegalSnapshotId_DisplayOrder',
                    'IX_tbl_ContractVersionPaymentMilestoneSnapshot_ContractVersionLegalSnapshotId_SourcePaymentMilestoneId'));
            """;
        Assert.Equal(13L, (long)(await schemaGuards.ExecuteScalarAsync())!);

        await using var generatedDefaults = connection.CreateCommand();
        generatedDefaults.CommandText =
            """
            SELECT COUNT_BIG(*)
            FROM sys.default_constraints AS d
            INNER JOIN sys.columns AS c
                ON c.object_id = d.parent_object_id
               AND c.column_id = d.parent_column_id
            INNER JOIN sys.tables AS t ON t.object_id = c.object_id
            WHERE t.name = 'tbl_ContractCustomerOtpDeliveryOutbox'
              AND c.name IN (
                'PhoneCiphertext', 'OtpCiphertext',
                'EmailCiphertext', 'DeliveryExpiresAt');
            """;
        Assert.Equal(0L, (long)(await generatedDefaults.ExecuteScalarAsync())!);

        await using var jsonConstraints = connection.CreateCommand();
        jsonConstraints.CommandText =
            """
            SELECT COUNT_BIG(*)
            FROM sys.check_constraints
            WHERE definition LIKE '%ISJSON%';
            """;
        Assert.Equal(0L, (long)(await jsonConstraints.ExecuteScalarAsync())!);

        await using var contractAuditFieldCodeConstraint =
            connection.CreateCommand();
        contractAuditFieldCodeConstraint.CommandText =
            """
            SELECT definition
            FROM sys.check_constraints
            WHERE name = 'CK_tbl_ContractAuditValue_FieldCode';
            """;
        var fieldCodeDefinition = Assert.IsType<string>(
            await contractAuditFieldCodeConstraint.ExecuteScalarAsync());
        Assert.Contains("[FieldCode]>=(1)", fieldCodeDefinition);
        Assert.Contains("[FieldCode]<=(99)", fieldCodeDefinition);

        await using var contractAuditFieldKindConstraint =
            connection.CreateCommand();
        contractAuditFieldKindConstraint.CommandText =
            """
            SELECT definition
            FROM sys.check_constraints
            WHERE name = 'CK_tbl_ContractAuditValue_FieldKind';
            """;
        var fieldKindDefinition = Assert.IsType<string>(
            await contractAuditFieldKindConstraint.ExecuteScalarAsync());
        Assert.Contains("95", fieldKindDefinition);
        Assert.Contains("98", fieldKindDefinition);

        await using var contractAuditSubjectConstraint =
            connection.CreateCommand();
        contractAuditSubjectConstraint.CommandText =
            """
            SELECT definition
            FROM sys.check_constraints
            WHERE name = 'CK_tbl_ContractAudit_Subject';
            """;
        var subjectDefinition = Assert.IsType<string>(
            await contractAuditSubjectConstraint.ExecuteScalarAsync());
        Assert.Contains("ContractAppendix", subjectDefinition);
        Assert.Contains("AcceptanceRecord", subjectDefinition);
    }

    private static TblEmployee Employee(
        string code,
        EmployeeType type) => new()
        {
            EmployeeCode = code,
            EmployeeAccount = code.ToLowerInvariant(),
            EmployeePassword = "phase-12-test-hash",
            EmployeeFullName = code,
            EmployeeType = (byte)type,
            Status = 1,
            DateCreated = DateTime.UtcNow
        };

    private static DbDtctechContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<DbDtctechContext>()
            .UseSqlServer(
                connectionString,
                sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure();
                    sqlOptions.MigrationsAssembly(
                        typeof(DbDtctechContext).Assembly.GetName().Name);
                })
            .Options;
        return new DbDtctechContext(options);
    }

    private static ContractAuditWriter CreateAuditWriter(
        DbDtctechContext context)
    {
        var tenant = new CurrentTenant();
        tenant.Set(new ResolvedTenant(
            1,
            "PHASE12-SQL",
            "Phase 12 SQL",
            TenantDatabaseMode.Dedicated,
            context.Database.GetConnectionString()!));
        return new ContractAuditWriter(
            context,
            tenant,
            new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext
                {
                    TraceIdentifier = "phase-12-sql-appendix"
                }
            });
    }

    private static string WithDatabase(
        string baseConnection,
        string databaseName)
    {
        var builder = new SqlConnectionStringBuilder(baseConnection)
        {
            InitialCatalog = databaseName
        };
        if (!databaseName.StartsWith(
                "ContractManagement_Phase12_QA_",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Phase 12 test refuses to use a non-QA database name.");
        }

        return builder.ConnectionString;
    }

    private static async Task DropQaDatabaseAsync(
        string connectionString,
        string databaseName)
    {
        if (!databaseName.StartsWith(
                "ContractManagement_Phase12_QA_",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Phase 12 cleanup refuses to delete a non-QA database.");
        }

        await using var context = CreateContext(connectionString);
        await context.Database.EnsureDeletedAsync();
    }

    private sealed record SeededTenantA(
        int ContractId,
        int OwnerId,
        int ManagerId,
        int UnrelatedEmployeeId);

    private sealed class Phase12SqlServerFactAttribute : FactAttribute
    {
        public Phase12SqlServerFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(
                    Environment.GetEnvironmentVariable(ConnectionVariable)))
            {
                Skip = $"Set {ConnectionVariable} to run the real SQL Server release gate.";
            }
        }
    }

    private sealed class ThrowingAuditWriter : IContractAuditWriter
    {
        public void StageAudits(
            IReadOnlyCollection<ContractAuditWriteRequest> requests) =>
            throw new InvalidOperationException(
                "Simulated audit staging failure.");

        public void StageEmployeeAudits(
            IReadOnlyCollection<EmployeeContractAuditWriteRequest> requests) =>
            throw new InvalidOperationException(
                "Simulated audit staging failure.");
    }
}
