using ContractManagement.API.Common.Enums;
using ContractManagement.API.Common.Security;
using ContractManagement.API.Domains.Discussions;
using ContractManagement.API.Domains.Models.Contract;
using ContractManagement.API.Domains.CustomerAccess;
using ContractManagement.API.Domains.DTOs.Requests.Contract;
using ContractManagement.API.Domains.DTOs.Requests.Public;
using ContractManagement.Domains.Interfaces.Contract;
using ContractManagement.Domains.Services.Contract;
using ContractManagement.Infrastructure.MultiTenancy.Enums;
using ContractManagement.Infrastructure.MultiTenancy.Models;
using ContractManagement.Infrastructure.MultiTenancy.Services;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using System.Text.Json;
using static ContractManagement.Tests.AuditValueTestData;

namespace ContractManagement.Tests.Domains.Services.Contract;

public sealed class ContractServiceSlice06Tests
{
    private const int TenantId = 906;
    private const int ContractId = 600;
    private const int VersionId = 601;
    private const int EmployeeId = 602;
    private const int CustomerId = 603;
    private const int TermId = 604;

    [Theory]
    [InlineData("Fake", ContractDirection.Sale)]
    [InlineData("Smtp", ContractDirection.Sale)]
    [InlineData("Fake", ContractDirection.Purchase)]
    [InlineData("Smtp", ContractDirection.Purchase)]
    public async Task DraftToNegotiating_OtpCustomerCommentAndNewRound_ShouldRevokeOldAccess(string provider, ContractDirection direction)
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        (await context.TblContracts.SingleAsync()).ContractDirection = (byte)direction;
        await context.SaveChangesAsync();
        var realtime = new RecordingDiscussionPublisher(context);
        var (contractService, customerAccess, cryptography) = CreateServices(context, provider, realtime);
        var rowVersion = Convert.ToBase64String(InitialRowVersion());

        var phone = await contractService.UpdateCustomerVerificationPhoneAsync(
            ContractId,
            new UpdateContractCustomerVerificationPhoneRequest
            {
                PhoneSource = "CustomerMobile",
                Reason = "Customer selected mobile",
                RowVersion = rowVersion
            },
            EmployeeId);
        Assert.Equal("********5678", phone.MaskedPhoneNumber);

        var link = await contractService.CreateCustomerAccessLinkAsync(
            ContractId,
            new CreateContractCustomerAccessLinkRequest { RowVersion = rowVersion },
            EmployeeId,
            "https://public.example.test");
        Assert.Equal("PendingActivation", link.State);
        var currentLink = await contractService.GetCurrentCustomerAccessLinkAsync(
            ContractId,
            EmployeeId);
        Assert.NotNull(currentLink);
        Assert.Equal(link.LinkId, currentLink.LinkId);
        Assert.Equal(VersionId, currentLink.VersionId);
        Assert.Equal("PendingActivation", currentLink.State);
        Assert.DoesNotContain(
            currentLink.GetType().GetProperties(),
            property => property.Name.Contains("Url", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Token", StringComparison.OrdinalIgnoreCase));

        var pendingToken = new Uri(link.PublicUrl).Segments.Last().Trim('/');
        var pendingAvailability = await customerAccess.GetLinkAvailabilityAsync(pendingToken);
        Assert.False(pendingAvailability.IsAvailable);
        Assert.Equal("PendingActivation", pendingAvailability.State);

        var pendingError = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            customerAccess.RequestOtpAsync(pendingToken, "+84912345678"));
        Assert.Equal(
            "Chỉ có thể xem hợp đồng khi hợp đồng đang ở trạng thái đàm phán.",
            pendingError.Message);
        Assert.Empty(context.TblContractCustomerOtpChallenges);

        await contractService.StartNegotiationAsync(
            ContractId,
            new StartContractNegotiationRequest { RowVersion = rowVersion },
            EmployeeId);

        var activeAvailability = await customerAccess.GetLinkAvailabilityAsync(pendingToken);
        Assert.True(activeAvailability.IsAvailable);
        Assert.Equal("Available", activeAvailability.State);

        var challengeResponse = await customerAccess.RequestOtpAsync(
            pendingToken,
            "+84912345678");
        var outbox = await context.TblContractCustomerOtpDeliveryOutbox.SingleAsync();
        var message = new CustomerOtpDeliveryMessage(
            cryptography.DecryptScalar(outbox.PhoneCiphertext),
            cryptography.DecryptScalar(outbox.OtpCiphertext),
            outbox.EmailCiphertext is null
                ? null
                : cryptography.DecryptScalar(outbox.EmailCiphertext),
            outbox.DeliveryExpiresAt);
        Assert.Equal(provider == "Smtp" ? "Email" : "Sms", challengeResponse.DeliveryChannel);
        Assert.Equal(provider == "Smtp" ? "customer@example.test" : null, message.EmailAddress);
        Assert.Equal((await context.TblContractCustomerOtpChallenges.SingleAsync()).ExpiresAt, message.ExpiresAt);
        Assert.Equal(message.ExpiresAt, outbox.DeliveryExpiresAt);
        if (provider == "Smtp") Assert.NotNull(outbox.EmailCiphertext);
        else Assert.Null(outbox.EmailCiphertext);
        var issue = await customerAccess.VerifyOtpAsync(
            pendingToken,
            challengeResponse.PublicChallengeId,
            message.Otp);

        var shared = await customerAccess.GetSharedAsync(issue.SessionSecret);
        var sharedItem = Assert.Single(shared.Items);
        Assert.Equal("Giấy phép", sharedItem.UnitName);
        Assert.Equal("License", sharedItem.UnitNameEn);
        Assert.Single(shared.Terms);
        Assert.Empty(shared.Comments);

        var comment = await customerAccess.CreateCommentAsync(
            issue.SessionSecret,
            new CreateCustomerNegotiationCommentRequest
            {
                TermId = TermId,
                Content = "Please clarify the term."
            });
        Assert.Equal("Customer", comment.Source);
        Assert.Equal("Open", comment.LifecycleState);
        Assert.Equal((ContractId, VersionId), Assert.Single(realtime.Publications));
        var session = await context.TblContractCustomerAccessSessions.SingleAsync();
        var idleExpiry = session.IdleExpiresAt;
        var lastActivity = session.LastActivityAt;
        var auditCount = await context.TblContractAudits.CountAsync();
        var discussionAccess = await customerAccess.GetDiscussionAccessAsync(issue.SessionSecret);
        Assert.Equal(ContractId, discussionAccess.ContractId);
        Assert.Equal(VersionId, discussionAccess.VersionId);
        Assert.Equal(idleExpiry, discussionAccess.ExpiresAt);
        Assert.Equal(comment.CommentId, Assert.Single(await customerAccess.GetCommentsAsync(issue.SessionSecret)).CommentId);
        Assert.Equal(idleExpiry, session.IdleExpiresAt);
        Assert.Equal(lastActivity, session.LastActivityAt);
        Assert.Equal(auditCount, await context.TblContractAudits.CountAsync());

        var persisted = await context.TblContractNegotiationComments.SingleAsync();
        Assert.Null(persisted.RecordedByEmployeeId);
        Assert.NotNull(persisted.CustomerAccessSessionId);
        Assert.Contains(context.TblContractAudits, x =>
            x.ActionType == ContractAuditActionTypes.CustomerCommentCreated
            && x.ActorType == ContractAuditActorTypes.Customer);

        var round = await contractService.CreateNegotiationRoundAsync(
            ContractId,
            new CreateContractNegotiationRoundRequest
            {
                CurrentVersionId = VersionId,
                RowVersion = rowVersion,
                CurrentVersionRowVersion = rowVersion,
                ChangeNote = "Customer negotiation round two"
            },
            EmployeeId);

        Assert.NotEqual(VersionId, round.CurrentVersion.VersionId);
        Assert.Null((await context.TblContracts.SingleAsync()).CurrentCustomerAccessLinkId);
        Assert.Null(await contractService.GetCurrentCustomerAccessLinkAsync(
            ContractId,
            EmployeeId));
        Assert.NotNull((await context.TblContractCustomerAccessLinks.SingleAsync()).RevokedAt);
        Assert.NotNull((await context.TblContractCustomerAccessSessions.SingleAsync()).RevokedAt);
        var revokedAvailability = await customerAccess.GetLinkAvailabilityAsync(pendingToken);
        Assert.False(revokedAvailability.IsAvailable);
        Assert.Equal("Unavailable", revokedAvailability.State);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => customerAccess.GetSharedAsync(issue.SessionSecret));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => customerAccess.GetDiscussionAccessAsync(issue.SessionSecret));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => customerAccess.GetCommentsAsync(issue.SessionSecret));
    }

    [Fact]
    public async Task VerificationPhoneChange_ShouldAuditBeforeAfterAndRevokedLink()
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        var (contractService, _, _) = CreateServices(context);
        var rowVersion = Convert.ToBase64String(InitialRowVersion());

        await contractService.UpdateCustomerVerificationPhoneAsync(
            ContractId,
            new UpdateContractCustomerVerificationPhoneRequest
            {
                PhoneSource = "CustomerMobile",
                Reason = "Chọn số khách hàng",
                RowVersion = rowVersion
            },
            EmployeeId);
        var link = await contractService.CreateCustomerAccessLinkAsync(
            ContractId,
            new CreateContractCustomerAccessLinkRequest
            {
                RowVersion = rowVersion
            },
            EmployeeId,
            "https://public.example.test");

        await contractService.UpdateCustomerVerificationPhoneAsync(
            ContractId,
            new UpdateContractCustomerVerificationPhoneRequest
            {
                PhoneSource = "Manual",
                ManualPhoneNumber = "+84987654321",
                Reason = "Khách hàng đổi số",
                RowVersion = rowVersion
            },
            EmployeeId);

        var phoneAudit = await context.TblContractAudits.SingleAsync(x =>
            x.ActionType == ContractAuditActionTypes.VerificationPhoneChanged);
        Assert.Equal(
            "********5678",
            ContractValue(phoneAudit, AuditValueSide.Previous,
                ContractAuditFieldCode.VerificationPhoneMasked).StringValue);
        Assert.Equal(
            "********4321",
            ContractValue(phoneAudit, AuditValueSide.New,
                ContractAuditFieldCode.VerificationPhoneMasked).StringValue);
        Assert.Equal(
            link.LinkId,
            ContractValue(phoneAudit, AuditValueSide.Previous,
                ContractAuditFieldCode.LinkId).IntegerValue);

        var revokedAudit = await context.TblContractAudits.SingleAsync(x =>
            x.ActionType == ContractAuditActionTypes.CustomerAccessLinkRevoked
            && x.SubjectId == link.LinkId);
        Assert.Equal(
            "Revoked",
            ContractValue(revokedAudit, AuditValueSide.New,
                ContractAuditFieldCode.LinkState).StringValue);
        Assert.Null((await context.TblContracts.SingleAsync())
            .CurrentCustomerAccessLinkId);
    }

    [Fact]
    public async Task ReplaceLink_ShouldAuditPreviousAndNewLinkIds()
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        var (contractService, _, _) = CreateServices(context);
        var rowVersion = Convert.ToBase64String(InitialRowVersion());

        await contractService.UpdateCustomerVerificationPhoneAsync(
            ContractId,
            new UpdateContractCustomerVerificationPhoneRequest
            {
                PhoneSource = "CustomerMobile",
                Reason = "Chọn số khách hàng",
                RowVersion = rowVersion
            },
            EmployeeId);
        var previous = await contractService.CreateCustomerAccessLinkAsync(
            ContractId,
            new CreateContractCustomerAccessLinkRequest
            {
                RowVersion = rowVersion
            },
            EmployeeId,
            "https://public.example.test");
        var replacement = await contractService.ReplaceCustomerAccessLinkAsync(
            ContractId,
            previous.LinkId,
            new ReplaceContractCustomerAccessLinkRequest
            {
                RowVersion = rowVersion,
                Reason = "Cấp lại link"
            },
            EmployeeId,
            "https://public.example.test");

        var audit = await context.TblContractAudits.SingleAsync(x =>
            x.ActionType == ContractAuditActionTypes.CustomerAccessLinkReplaced);
        Assert.Equal(
            previous.LinkId,
            ContractValue(audit, AuditValueSide.Previous,
                ContractAuditFieldCode.PreviousLinkId).IntegerValue);
        Assert.Equal(
            replacement.LinkId,
            ContractValue(audit, AuditValueSide.New,
                ContractAuditFieldCode.NewLinkId).IntegerValue);
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("missing-cookie")]
    [InlineData("wrong-secret")]
    [InlineData("wrong-tenant")]
    [InlineData("expired-idle")]
    [InlineData("expired-hard")]
    [InlineData("revoked-session")]
    [InlineData("revoked-link")]
    [InlineData("expired-link")]
    [InlineData("pending-link")]
    [InlineData("changed-version")]
    [InlineData("locked-version")]
    [InlineData("closed-negotiation")]
    public async Task DiscussionCustomerAccess_RequiresActiveOtpAndCurrentNegotiatingVersion(string scenario)
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        var (_, access, crypto) = CreateServices(context);
        var now = DateTime.UtcNow;
        var contract = await context.TblContracts.SingleAsync();
        contract.Status = (byte)ContractStatus.Negotiating;
        var link = new TblContractCustomerAccessLink
        {
            CustomerAccessLinkId = 900, TenantId = TenantId, ContractId = ContractId,
            VersionId = VersionId, VerificationPhoneId = 901, TokenHash = crypto.HashSecret("link-secret"),
            CreatedByEmployeeId = EmployeeId, CreatedDate = now, ActivatedAt = now,
            ExpiresAt = now.AddHours(1), RowVersion = InitialRowVersion()
        };
        var session = new TblContractCustomerAccessSession
        {
            CustomerAccessSessionId = 902, TenantId = TenantId, ContractId = ContractId,
            VersionId = VersionId, LinkId = 900, VerificationPhoneId = 901,
            SessionTokenHash = crypto.HashSecret("session-secret"), IssuedAt = now, LastActivityAt = now,
            IdleExpiresAt = now.AddMinutes(30), HardExpiresAt = now.AddHours(1), RowVersion = InitialRowVersion()
        };
        var secret = "session-secret";
        switch (scenario)
        {
            case "missing-cookie": secret = ""; break;
            case "wrong-secret": secret = "other-session"; break;
            case "wrong-tenant": session.TenantId++; break;
            case "expired-idle": session.IdleExpiresAt = now.AddMinutes(-1); break;
            case "expired-hard": session.HardExpiresAt = now.AddMinutes(-1); break;
            case "revoked-session": session.RevokedAt = now; break;
            case "revoked-link": link.RevokedAt = now; break;
            case "expired-link": link.ExpiresAt = now.AddMinutes(-1); break;
            case "pending-link": link.ActivatedAt = null; break;
            case "changed-version": contract.CurrentVersionId++; break;
            case "locked-version":
                var version = await context.TblContractVersions.SingleAsync();
                var snapshot = ContractSnapshotTestData.Create(ContractId, VersionId);
                version.IsLocked = true;
                version.SnapshotHash = SoftwareSupplyContractSnapshotFactory.CalculateHash(snapshot);
                context.TblContractVersionLegalSnapshots.Add(
                    SoftwareSupplyContractSnapshotFactory.CreatePersistenceGraph(snapshot, EmployeeId, now));
                break;
            case "closed-negotiation": contract.Status = (byte)ContractStatus.Draft; break;
        }
        context.TblContractCustomerAccessLinks.Add(link);
        context.TblContractCustomerAccessSessions.Add(session);
        for (var index = 0; index < 4; index++)
            context.TblContractNegotiationComments.Add(new TblContractNegotiationComment
            {
                CommentId = 910 + index, ContractId = index == 2 ? ContractId + 1 : ContractId,
                VersionId = index == 3 ? VersionId + 1 : VersionId,
                ParentCommentId = index == 1 ? 910 : null, Content = $"Comment {index}",
                Source = "Customer", CustomerAccessSessionId = 902, CreatedDate = now,
                RowVersion = InitialRowVersion()
            });
        await context.SaveChangesAsync();
        if (scenario != "valid")
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => access.GetDiscussionAccessAsync(secret));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => access.GetCommentsAsync(secret));
            return;
        }
        var subscription = await access.GetDiscussionAccessAsync(secret);
        Assert.Equal(ContractId, subscription.ContractId);
        Assert.Equal(VersionId, subscription.VersionId);
        var comments = await access.GetCommentsAsync(secret);
        Assert.Equal(new[] { 910, 911 }, comments.Select(x => x.CommentId));
        Assert.Equal(910, comments[1].ParentCommentId);
        var json = JsonSerializer.Serialize(comments);
        Assert.DoesNotContain("Session", json);
        Assert.DoesNotContain("Employee", json);
        Assert.DoesNotContain("RowVersion", json);
        Assert.Equal(now, session.LastActivityAt);
        Assert.Equal(now.AddMinutes(30), session.IdleExpiresAt);
        Assert.Empty(context.TblContractAudits);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("manager")]
    [InlineData("other-employee")]
    [InlineData("wrong-tenant")]
    [InlineData("wrong-version")]
    [InlineData("expired-session")]
    public async Task DiscussionEmployeeAccess_UsesExistingSessionAndContractReadPermissions(string scenario)
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        var employee = await context.TblEmployees.SingleAsync();
        employee.SessionVersion = 1;
        if (scenario == "manager") employee.EmployeeType = (byte)EmployeeType.Manager;
        if (scenario is "manager" or "other-employee") (await context.TblContracts.SingleAsync()).EmployeeId++;
        await context.SaveChangesAsync();
        var tenant = new CurrentTenant();
        tenant.Set(new ResolvedTenant(TenantId, "tenant-906", "Tenant 906", TenantDatabaseMode.Dedicated, "InMemory"));
        var (_, customerAccess, _) = CreateServices(context);
        var access = new ContractDiscussionAccessService(tenant, context, customerAccess);
        var http = new DefaultHttpContext { Session = new DiscussionTestSession() };
        http.Session.SetInt32(AccountSessionKeys.EmployeeId, EmployeeId);
        http.Session.SetInt32(AccountSessionKeys.EmployeeSessionVersion, scenario == "expired-session" ? 0 : 1);
        http.Session.SetInt32("TenantId", scenario == "wrong-tenant" ? TenantId + 1 : TenantId);
        http.Request.RouteValues["contractId"] = ContractId;
        http.Request.RouteValues["versionId"] = scenario == "wrong-version" ? VersionId + 1 : VersionId;
        if (scenario is "wrong-tenant" or "expired-session")
            await Assert.ThrowsAsync<RbacOperationException>(() => access.ForEmployeeAsync(http));
        else if (scenario is "other-employee" or "wrong-version")
            await Assert.ThrowsAsync<KeyNotFoundException>(() => access.ForEmployeeAsync(http));
        else
        {
            var subscription = await access.ForEmployeeAsync(http);
            Assert.Equal(TenantId, subscription.TenantId);
            Assert.Equal(ContractId, subscription.ContractId);
            Assert.Equal(VersionId, subscription.VersionId);
        }
    }

    private sealed class DiscussionTestSession : ISession
    {
        private readonly Dictionary<string, byte[]> values = [];
        public bool IsAvailable => true;
        public string Id => "discussion-test";
        public IEnumerable<string> Keys => values.Keys;
        public void Clear() => values.Clear();
        public void Remove(string key) => values.Remove(key);
        public void Set(string key, byte[] value) => values[key] = value;
        public bool TryGetValue(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out byte[]? value) =>
            values.TryGetValue(key, out value);
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static DbDtctechContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DbDtctechContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(
                InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new DbDtctechContext(options);
    }

    private static (ContractService ContractService,
        CustomerContractAccessService CustomerAccess,
        CustomerAccessCryptography Cryptography) CreateServices(DbDtctechContext context, string provider = "Fake",
            IContractDiscussionRealtimePublisher? realtime = null)
    {
        var tenant = new CurrentTenant();
        tenant.Set(new ResolvedTenant(
            TenantId,
            "tenant-906",
            "Tenant 906",
            TenantDatabaseMode.Dedicated,
            "InMemory"));
        var audit = new ContractAuditWriter(
            context,
            tenant,
            new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = "slice-06" }
            });
        var options = Options.Create(new CustomerOtpOptions
        {
            Provider = provider,
            HashKey = Convert.ToBase64String(Enumerable.Repeat((byte)1, 32).ToArray()),
            EncryptionKey = Convert.ToBase64String(Enumerable.Repeat((byte)2, 32).ToArray())
        });
        var cryptography = new CustomerAccessCryptography(options);
        var contractService = new ContractService(context, audit, tenant, cryptography, realtime);
        return (
            contractService,
            new CustomerContractAccessService(
                context,
                tenant,
                cryptography,
                contractService,
                audit,
                options),
            cryptography);
    }

    [Theory]
    [InlineData(null, "+84912345678")]
    [InlineData("invalid-email", "+84912345678")]
    [InlineData("customer@example.test", "+84999999999")]
    public async Task Smtp_InvalidRecipientOrWrongPhone_DoesNotEnqueueOrDiscloseEmail(
        string? email, string suppliedPhone)
    {
        await using var context = CreateContext();
        await SeedAsync(context);
        (await context.TblCustomers.SingleAsync()).CustomerEmail = email;
        await context.SaveChangesAsync();
        var (contracts, access, _) = CreateServices(context, "Smtp");
        var rowVersion = Convert.ToBase64String(InitialRowVersion());
        await contracts.UpdateCustomerVerificationPhoneAsync(ContractId,
            new UpdateContractCustomerVerificationPhoneRequest
            {
                PhoneSource = "CustomerMobile", Reason = "Select contact", RowVersion = rowVersion
            }, EmployeeId);
        var link = await contracts.CreateCustomerAccessLinkAsync(ContractId,
            new CreateContractCustomerAccessLinkRequest { RowVersion = rowVersion },
            EmployeeId, "https://public.example.test");
        await contracts.StartNegotiationAsync(ContractId,
            new StartContractNegotiationRequest { RowVersion = rowVersion }, EmployeeId);
        var token = new Uri(link.PublicUrl).Segments.Last().Trim('/');
        var response = await access.RequestOtpAsync(token, suppliedPhone);
        Assert.Equal("Email", response.DeliveryChannel);
        Assert.False(string.IsNullOrWhiteSpace(response.PublicChallengeId));
        Assert.Empty(context.TblContractCustomerOtpDeliveryOutbox);
        Assert.Empty(context.TblContractCustomerOtpChallenges);
        Assert.DoesNotContain("@", JsonSerializer.Serialize(response));
    }

    private static async Task SeedAsync(DbDtctechContext context)
    {
        context.TblEmployees.Add(new TblEmployee
        {
            EmployeeId = EmployeeId,
            EmployeeAccount = "slice06-owner",
            EmployeeFullName = "Slice 06 owner",
            EmployeeType = (byte)EmployeeType.Sale,
            Status = 1
        });
        context.TblCustomers.Add(new TblCustomer
        {
            CustomerId = CustomerId,
            CustomerFullName = "Slice 06 customer",
            CustomerCompany = "Slice 06 Customer Company",
            CustomerAddress = "Ha Noi",
            CustomerRepresentativeName = "Slice 06 Representative",
            CustomerRepresentativeTitle = "Director",
            CustomerMobile = "+84912345678",
            CustomerEmail = "customer@example.test",
            Status = 1
        });
        context.TblTenantLegalProfiles.Add(new TblTenantLegalProfile
        {
            TenantLegalProfileId = 1,
            LegalEntityName = "DTC Company",
            TaxCode = "0100000006",
            Address = "Ho Chi Minh City",
            RepresentativeName = "Provider Representative",
            RepresentativeTitle = "General Director",
            CreatedByEmployeeId = EmployeeId,
            UpdatedByEmployeeId = EmployeeId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = InitialRowVersion()
        });
        context.TblContracts.Add(new TblContract
        {
            ContractDirection = (byte)1,
            ContractId = ContractId,
            CustomerId = CustomerId,
            EmployeeId = EmployeeId,
            ContractType = 1,
            CurrentVersionId = VersionId,
            ContractCode = "HD-S06",
            ContractName = "Slice 06",
            Status = (byte)ContractStatus.Draft,
            CurrencyCode = "VND",
            LanguageMode = 1,
            CreatedEmployeeId = EmployeeId,
            CreatedDate = DateTime.UtcNow,
            RowVersion = InitialRowVersion()
        });
        context.TblContractVersions.Add(new TblContractVersion
        {
            VersionId = VersionId,
            ContractId = ContractId,
            VersionNo = 1,
            CurrencyCode = "VND",
            IsLocked = false,
            CreatedEmployeeId = EmployeeId,
            CreatedDate = DateTime.UtcNow,
            RowVersion = InitialRowVersion()
        });
        context.TblContractItems.Add(new TblContractItem
        {
            ContractItemId = 605,
            ContractId = ContractId,
            VersionId = VersionId,
            ItemType = 1,
            ItemName = "Slice 06 item",
            UnitName = "Giấy phép",
            UnitNameEn = "License",
            Quantity = 1,
            UnitPrice = 100,
            LineSubtotal = 100,
            DiscountMode = 0,
            DiscountPercent = 0,
            FixedDiscountAmount = 0,
            DiscountAmount = 0,
            IsTaxable = false,
            VatPercent = 0,
            VatAmount = 0,
            LineTotal = 100,
            DisplayOrder = 1,
            CreatedEmployeeId = EmployeeId,
            CreatedDate = DateTime.UtcNow,
            RowVersion = InitialRowVersion()
        });
        context.TblContractTerms.Add(new TblContractTerm
        {
            TermId = TermId,
            ContractId = ContractId,
            VersionId = VersionId,
            TermCode = "NEGOTIABLE",
            TermTitle = "Negotiable term",
            IsNegotiable = true,
            DisplayOrder = 1,
            CreatedEmployeeId = EmployeeId,
            CreatedDate = DateTime.UtcNow,
            RowVersion = InitialRowVersion()
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private static byte[] InitialRowVersion() => [1, 2, 3, 4, 5, 6, 7, 8];
}
