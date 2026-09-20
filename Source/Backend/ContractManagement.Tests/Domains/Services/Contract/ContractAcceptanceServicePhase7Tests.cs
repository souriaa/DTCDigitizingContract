using System.Security.Cryptography;
using ContractManagement.API.Common.Enums;
using ContractManagement.API.Common.Exceptions;
using ContractManagement.API.Domains.DTOs.Requests.Contract;
using ContractManagement.Common.Enums;
using ContractManagement.Domains.Interfaces.Contract;
using ContractManagement.Domains.Interfaces.ContractTemplate;
using ContractManagement.Domains.Interfaces.File;
using ContractManagement.Domains.Policies.ContractTemplate;
using ContractManagement.Domains.Services.Contract;
using ContractManagement.Infrastructure.MultiTenancy.Enums;
using ContractManagement.Infrastructure.MultiTenancy.Models;
using ContractManagement.Infrastructure.MultiTenancy.Services;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using W = DocumentFormat.OpenXml.Wordprocessing;
using Xunit;
using static ContractManagement.Tests.ContractRichTextTestData;

namespace ContractManagement.Tests.Domains.Services.Contract;

public sealed class ContractAcceptanceServicePhase7Tests
{
    private const int ContractId = 71;
    private const int VersionId = 72;
    private const int TemplateId = 73;
    private const int TemplateVersionId = 74;
    private const int EmployeeId = 75;
    private const int AppendixId = 76;
    private const int MilestoneId = 77;
    private static readonly byte[] InitialVersion = [1, 2, 3, 4, 5, 6, 7, 8];

    [Fact]
    public async Task Lifecycle_UsesRelationalSnapshot_Artifacts_AndSignedGate()
    {
        await using var db = CreateContext();
        var storage = new TrackingStorage();
        await SeedAsync(db, storage);
        var audit = new CapturingAuditWriter();
        var service = CreateService(db, storage, audit);

        var draft = await service.CreateAsync(
            ContractId, CreateRequest(), EmployeeId);

        Assert.Equal(AcceptanceRecordStatus.Draft, draft.Status);
        Assert.Equal(2, draft.References.Count);
        Assert.Equal(AcceptanceReferenceType.Contract,
            draft.References[0].ReferenceType);
        Assert.Contains("ký ngày 12/09/2026", draft.References[0].TextVi);
        Assert.DoesNotContain("ký ngày", draft.References[1].TextVi);
        Assert.Equal(2, draft.Parties.Count);
        var customerParty = Assert.Single(draft.Parties,
            party => party.PartyRole == AcceptancePartyRole.Customer);
        Assert.Equal("Người liên hệ nghiệm thu",
            customerParty.RepresentativeName);
        Assert.Equal("Điều phối dự án", customerParty.RepresentativeTitle);
        Assert.Single(draft.Sections);
        Assert.Single(draft.Milestones);
        Assert.Contains(audit.EmployeeRequests, x =>
            x.ActionType == ContractAuditActionTypes.AcceptanceRecordCreated);

        var preview = await service.PreviewAsync(
            ContractId, draft.AcceptanceRecordId, "docx", EmployeeId);
        Assert.True(preview.Content.Length > 0);

        var finalized = await service.FinalizeAsync(
            ContractId, draft.AcceptanceRecordId,
            new() { RowVersion = draft.RowVersion }, EmployeeId);

        Assert.Equal(AcceptanceRecordStatus.Finalized, finalized.Status);
        Assert.NotNull(finalized.SnapshotHash);
        Assert.Equal(64, finalized.SnapshotHash!.Length);
        Assert.Equal(["docx", "pdf"],
            finalized.Artifacts.Select(x => x.Format).Order().ToArray());
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ReplaceReferencesAsync(ContractId,
                draft.AcceptanceRecordId,
                new() { RowVersion = finalized.RowVersion }, EmployeeId));

        var evidence = await service.UploadSignedEvidenceAsync(
            ContractId, draft.AcceptanceRecordId,
            new()
            {
                RowVersion = finalized.RowVersion,
                File = FormFile("signed.pdf", "application/pdf", "%PDF-signed")
            }, EmployeeId);
        var signed = await service.GetAsync(
            ContractId, draft.AcceptanceRecordId, EmployeeId);

        Assert.Equal(AcceptanceRecordStatus.Signed, signed.Status);
        Assert.Equal(evidence.AcceptanceEvidenceId,
            signed.SignedEvidence!.AcceptanceEvidenceId);
        var milestone = await db.TblContractPaymentMilestones
            .SingleAsync(x => x.PaymentMilestoneId == MilestoneId);
        Assert.Equal(signed.SignedAt!.Value.Date, milestone.AnchorDate);
        Assert.Equal(milestone.AnchorDate!.Value.AddDays(5), milestone.DueDate);
        var outsideScope = await db.TblContractPaymentMilestones
            .SingleAsync(x => x.PaymentMilestoneId == MilestoneId + 1);
        Assert.Null(outsideScope.AnchorDate);
        Assert.Contains(audit.EmployeeRequests, x =>
            x.ActionType == ContractAuditActionTypes.AcceptanceRecordFinalized);
        Assert.Contains(audit.EmployeeRequests, x =>
            x.ActionType == ContractAuditActionTypes.AcceptanceEvidenceUploaded);
        var evidenceAudit = Assert.Single(audit.EmployeeRequests, x =>
            x.ActionType == ContractAuditActionTypes.AcceptanceEvidenceUploaded);
        Assert.Equal(ContractAuditSubjectTypes.AcceptanceEvidence,
            evidenceAudit.SubjectType);
        Assert.Equal(evidence.AcceptanceEvidenceId, evidenceAudit.SubjectId);
        Assert.True(evidenceAudit.SubjectId > 0);
        var auditedStrings = audit.EmployeeRequests
            .SelectMany(request => request.NewValues ?? [])
            .Where(value => value.StringValue is not null)
            .Select(value => value.StringValue!);
        Assert.DoesNotContain(auditedStrings, value =>
            value.Contains("Đã hoàn thành công việc", StringComparison.Ordinal)
            || value.Contains("contract-rich-text", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Preview_AllowsOptionalBlocks_AndRendersSignaturesAndCustomPlaceholder()
    {
        await using var db = CreateContext();
        var storage = new TrackingStorage();
        await SeedAsync(db, storage);
        var customer = await db.TblCustomers.SingleAsync();
        customer.CustomerContactPersonName = "Người liên hệ nghiệm thu";
        db.TblContractTemplateFields.Add(new TblContractTemplateField
        {
            TemplateVersionId = TemplateVersionId,
            PlaceholderKey = "ACCEPTANCE_CONTACT",
            FieldLabel = "Người liên hệ nghiệm thu",
            DataSource = "Khách hàng / Người liên hệ",
            SourceFieldKey = "customer.contact-name",
            DataKind = (byte)TemplatePlaceholderDataKind.Scalar,
            Multiplicity = (byte)TemplatePlaceholderMultiplicity.ZeroOrOne,
            IsSystem = false,
            IsRequired = false,
            DisplayOrder = 0,
            CreatedEmployeeId = EmployeeId,
            CreatedDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        storage.Add("templates/acceptance.docx",
            CreateTemplateDocxWithCustomAndSignatures());
        var service = CreateService(db, storage);
        var draft = await service.CreateAsync(
            ContractId, CreateRequest(), EmployeeId);

        var preview = await service.PreviewAsync(
            ContractId, draft.AcceptanceRecordId, "docx", EmployeeId);

        using var document = WordprocessingDocument.Open(
            new MemoryStream(preview.Content), false);
        var text = document.MainDocumentPart!.Document!.InnerText;
        Assert.Contains("Người liên hệ nghiệm thu", text);
        Assert.Contains("ĐẠI DIỆN BÊN A", text);
        Assert.Contains("Người liên hệ nghiệm thu", text);
        var customerSignature = document.MainDocumentPart.Document.Body!
            .Elements<W.Paragraph>()
            .Single(paragraph => paragraph.InnerText ==
                "Người liên hệ nghiệm thu");
        Assert.Equal(W.JustificationValues.Center,
            customerSignature.ParagraphProperties?
                .GetFirstChild<W.Justification>()?.Val?.Value);
        Assert.All(customerSignature.Descendants<W.Run>(), run =>
            Assert.Equal("Arial", run.RunProperties?
                .GetFirstChild<W.RunFonts>()?.Ascii?.Value));
        var contactLine = document.MainDocumentPart.Document.Body
            .Elements<W.Paragraph>()
            .Single(paragraph => paragraph.InnerText ==
                "Liên hệ: Người liên hệ nghiệm thu");
        Assert.Equal("Arial", contactLine.Descendants<W.Run>().First()
            .RunProperties?.GetFirstChild<W.RunFonts>()?.Ascii?.Value);
        Assert.Contains("ĐẠI DIỆN BÊN B", text);
        Assert.Contains("Nguyễn Văn B", text);
        Assert.DoesNotContain("{{", text);
    }

    [Fact]
    public async Task Create_RejectsMissingSignDate_AndAppendixOutsideSignedPackage()
    {
        await using var db = CreateContext();
        var storage = new TrackingStorage();
        await SeedAsync(db, storage);
        var service = CreateService(db, storage);
        var contract = await db.TblContracts.SingleAsync();
        contract.SignDate = null;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(ContractId, CreateRequest(), EmployeeId));

        contract = await db.TblContracts.SingleAsync();
        contract.SignDate = new DateTime(2026, 9, 12);
        await db.SaveChangesAsync();
        var request = CreateRequest();
        request.AppendixIds = [999999];
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(ContractId, request, EmployeeId));
        Assert.Equal("AcceptanceAppendixSelectionInvalid", exception.Code);
    }

    [Fact]
    public async Task Mutations_EnforceRbac_AndRowVersion()
    {
        await using var db = CreateContext();
        var storage = new TrackingStorage();
        await SeedAsync(db, storage);
        var denied = CreateService(db, storage,
            authorization: new TestAuthorization(denyManage: true));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            denied.CreateAsync(ContractId, CreateRequest(), EmployeeId));

        var service = CreateService(db, storage);
        var draft = await service.CreateAsync(
            ContractId, CreateRequest(), EmployeeId);
        var stale = Convert.ToBase64String([9, 9, 9, 9]);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            service.ReplaceMilestonesAsync(ContractId,
                draft.AcceptanceRecordId,
                new() { RowVersion = stale, PaymentMilestoneIds = [MilestoneId] },
                EmployeeId));
    }

    [Fact]
    public async Task Finalize_WhenSecondArtifactSaveFails_DeletesFirstFile()
    {
        await using var db = CreateContext();
        var storage = new TrackingStorage();
        await SeedAsync(db, storage);
        var service = CreateService(db, storage);
        var draft = await service.CreateAsync(
            ContractId, CreateRequest(), EmployeeId);
        storage.FailOnSaveNumber = 2;

        await Assert.ThrowsAsync<IOException>(() => service.FinalizeAsync(
            ContractId, draft.AcceptanceRecordId,
            new() { RowVersion = draft.RowVersion }, EmployeeId));

        Assert.Single(storage.DeletedKeys);
        var row = await db.TblContractAcceptanceRecords.AsNoTracking()
            .SingleAsync();
        Assert.Equal((byte)AcceptanceRecordStatus.Draft, row.Status);
        Assert.Empty(db.TblFileStorages.Where(x =>
            x.ObjectType == "ContractAcceptanceArtifact"));
    }

    [Fact]
    public async Task Finalize_RechecksActiveSignedSource()
    {
        await using var db = CreateContext();
        var storage = new TrackingStorage();
        await SeedAsync(db, storage);
        var service = CreateService(db, storage);
        var draft = await service.CreateAsync(
            ContractId, CreateRequest(), EmployeeId);
        var contract = await db.TblContracts.SingleAsync();
        contract.SignDate = null;
        await db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.FinalizeAsync(ContractId, draft.AcceptanceRecordId,
                new() { RowVersion = draft.RowVersion }, EmployeeId));
        Assert.Equal("ContractSignDateRequired", exception.Code);
        Assert.Empty(storage.SavedKeys);
    }

    [Fact]
    public async Task SignedEvidence_WhenRowVersionIsStale_DeletesUploadedFile()
    {
        await using var db = CreateContext();
        var storage = new TrackingStorage();
        await SeedAsync(db, storage);
        var service = CreateService(db, storage);
        var draft = await service.CreateAsync(
            ContractId, CreateRequest(), EmployeeId);
        var finalized = await service.FinalizeAsync(
            ContractId, draft.AcceptanceRecordId,
            new() { RowVersion = draft.RowVersion }, EmployeeId);
        var deletedBefore = storage.DeletedKeys.Count;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            service.UploadSignedEvidenceAsync(
                ContractId,
                finalized.AcceptanceRecordId,
                new()
                {
                    RowVersion = Convert.ToBase64String([9, 8, 7, 6]),
                    File = FormFile(
                        "signed.pdf", "application/pdf", "%PDF-stale")
                },
                EmployeeId));

        Assert.Equal(deletedBefore + 1, storage.DeletedKeys.Count);
        Assert.Empty(db.TblContractAcceptanceEvidences);
        var row = await db.TblContractAcceptanceRecords.AsNoTracking()
            .SingleAsync();
        Assert.Equal((byte)AcceptanceRecordStatus.Finalized, row.Status);
    }

    private static DbDtctechContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DbDtctechContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), options =>
                options.EnableNullChecks(false))
            .ConfigureWarnings(warnings => warnings.Ignore(
                InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new DbDtctechContext(options);
    }

    private static ContractAcceptanceService CreateService(
        DbDtctechContext db,
        TrackingStorage storage,
        CapturingAuditWriter? audit = null,
        IContractResourceAuthorizationService? authorization = null)
    {
        var tenant = new CurrentTenant();
        tenant.Set(new ResolvedTenant(1, "TENANT-PHASE7", "Tenant Phase 7",
            TenantDatabaseMode.Dedicated, "InMemory"));
        return new ContractAcceptanceService(
            db,
            authorization ?? new TestAuthorization(),
            audit ?? new CapturingAuditWriter(),
            storage,
            tenant,
            new FakePdfRenderer(),
            new CalendarDueDateService());
    }

    private static CreateContractAcceptanceRecordRequest CreateRequest() => new()
    {
        ContractVersionId = VersionId,
        TemplateVersionId = TemplateVersionId,
        AcceptanceCode = "BBNT-001",
        AcceptanceDate = new DateTime(2026, 9, 18),
        Location = "Hà Nội",
        AcceptanceKind = AcceptanceKind.Partial,
        AppendixIds = [AppendixId],
        PaymentMilestoneIds = [MilestoneId],
        ContractRowVersion = Convert.ToBase64String(InitialVersion),
        VersionRowVersion = Convert.ToBase64String(InitialVersion)
    };

    private static async Task SeedAsync(
        DbDtctechContext db, TrackingStorage storage)
    {
        var now = new DateTime(2026, 9, 18, 8, 0, 0, DateTimeKind.Utc);
        var templateDocx = CreateTemplateDocx();
        storage.Add("templates/acceptance.docx", templateDocx);
        db.TblEmployees.Add(new TblEmployee
        {
            EmployeeId = EmployeeId,
            EmployeeFullName = "Nguyễn Văn B",
            Status = 1,
            RowVersion = InitialVersion.ToArray()
        });
        db.TblCustomers.Add(new TblCustomer
        {
            CustomerId = 1,
            CustomerFullName = "Khách hàng A",
            CustomerContactPersonName = "Người liên hệ nghiệm thu",
            CustomerContactPersonTitle = "Điều phối dự án",
            Status = 1
        });
        db.TblFileStorages.AddRange(
            new TblFileStorage
            {
                FileId = 1,
                ObjectType = "ContractTemplateDocument",
                ObjectId = TemplateVersionId,
                FileName = "acceptance.docx",
                FilePath = string.Empty,
                StorageKey = "templates/acceptance.docx",
                TenantCode = "TENANT-PHASE7",
                ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                FileType = "docx",
                FileSize = templateDocx.Length,
                Sha256 = Sha(templateDocx),
                UploadedDate = now
            },
            new TblFileStorage
            {
                FileId = 2,
                ObjectType = "ContractSignedEvidence",
                ObjectId = ContractId,
                FileName = "contract-signed.pdf",
                FilePath = string.Empty,
                StorageKey = "contracts/signed.pdf",
                TenantCode = "TENANT-PHASE7",
                ContentType = "application/pdf",
                FileType = "pdf",
                FileSize = 11,
                Sha256 = new string('a', 64),
                UploadedDate = now
            });
        db.TblContractTemplates.Add(new TblContractTemplate
        {
            TemplateId = TemplateId,
            TemplateCode = "ACCEPTANCE_VI",
            TemplateName = "Biên bản nghiệm thu",
            DocumentType = (byte)TemplateDocumentType.AcceptanceRecord,
            LanguageMode = (byte)ContractLanguageMode.Vietnamese,
            CurrentPublishedVersionId = TemplateVersionId,
            IsActive = true,
            CreatedEmployeeId = EmployeeId,
            CreatedDate = now,
            RowVersion = InitialVersion.ToArray()
        });
        db.TblContractTemplateVersions.Add(new TblContractTemplateVersion
        {
            TemplateVersionId = TemplateVersionId,
            TemplateId = TemplateId,
            VersionNo = 1,
            Status = (byte)TemplateVersionStatus.Published,
            ValidationStatus = (byte)TemplateValidationStatus.Valid,
            DocumentFileId = 1,
            DocumentHash = Sha(templateDocx),
            CreatedEmployeeId = EmployeeId,
            CreatedDate = now,
            RowVersion = InitialVersion.ToArray()
        });
        db.TblContractTemplateTerms.Add(new TblContractTemplateTerm
        {
            TemplateTermId = 1,
            TemplateVersionId = TemplateVersionId,
            TermCode = "RESULT",
            TermTitle = "I. KẾT QUẢ THỰC HIỆN",
            TermContent = RichText("Đã hoàn thành công việc."),
            DisplayOrder = 1,
            CreatedEmployeeId = EmployeeId,
            CreatedDate = now,
            RowVersion = InitialVersion.ToArray()
        });
        db.TblContracts.Add(new TblContract
        {
            ContractId = ContractId,
            CustomerId = 1,
            EmployeeId = EmployeeId,
            ContractType = (byte)ContractType.SoftwareSupply,
            TemplateVersionId = 1,
            CurrentVersionId = VersionId,
            ContractCode = "HD-001",
            ContractName = "Cung cấp phần mềm",
            SignDate = new DateTime(2026, 9, 12),
            Status = (byte)ContractStatus.Signed,
            CurrencyCode = "VND",
            LanguageMode = (byte)ContractLanguageMode.Vietnamese,
            CreatedEmployeeId = EmployeeId,
            CreatedDate = now,
            RowVersion = InitialVersion.ToArray()
        });
        db.TblContractVersions.Add(new TblContractVersion
        {
            VersionId = VersionId,
            ContractId = ContractId,
            VersionNo = 1,
            TemplateVersionId = 1,
            CurrencyCode = "VND",
            IsLocked = false,
            CreatedEmployeeId = EmployeeId,
            CreatedDate = now,
            RowVersion = InitialVersion.ToArray()
        });
        db.TblContractSignedEvidences.Add(new TblContractSignedEvidence
        {
            SignedEvidenceId = 1,
            ContractId = ContractId,
            VersionId = VersionId,
            FileId = 2,
            Status = (byte)SignedEvidenceStatus.Active,
            UploadedByEmployeeId = EmployeeId,
            UploadedAt = now,
            RowVersion = InitialVersion.ToArray()
        });
        var legalSnapshot = new TblContractVersionLegalSnapshot
        {
            ContractVersionLegalSnapshotId = 1,
            VersionId = VersionId,
            ContractId = ContractId,
            VersionNo = 1,
            TemplateVersionId = 1,
            ContractCode = "HD-001",
            ContractName = "Cung cấp phần mềm",
            ContractType = (byte)ContractType.SoftwareSupply,
            ContractCreatedDate = now,
            SignDate = new DateTime(2026, 9, 12),
            CurrencyCode = "VND",
            LanguageMode = (byte)ContractLanguageMode.Vietnamese,
            CreatedDate = now,
            CreatedByEmployeeId = EmployeeId,
            Parties =
            [
                new TblContractVersionPartySnapshot
                {
                    ContractVersionPartySnapshotId = 1,
                    PartyRole = 1,
                    LegalName = "Công ty DTC",
                    Address = "Hà Nội",
                    RepresentativeName = "Nguyễn Văn B",
                    RepresentativeTitle = "Giám đốc"
                },
                new TblContractVersionPartySnapshot
                {
                    ContractVersionPartySnapshotId = 2,
                    PartyRole = 2,
                    LegalName = "Khách hàng A",
                    Address = "Bình Dương",
                    RepresentativeName = "Trần Văn A",
                    RepresentativeTitle = "Kế toán trưởng"
                }
            ]
        };
        db.TblContractAppendices.Add(new TblContractAppendix
        {
            AppendixId = AppendixId,
            ContractId = ContractId,
            VersionId = VersionId,
            SourceTemplateAppendixId = 1,
            AppendixCode = "PL-01",
            AppendixName = "Nâng cấp phần mềm",
            DisplayOrder = 1,
            CreatedEmployeeId = EmployeeId,
            CreatedDate = now,
            RowVersion = InitialVersion.ToArray()
        });
        db.TblContractPaymentMilestones.AddRange(
            Milestone(MilestoneId, "DOT-01", 1, now),
            Milestone(MilestoneId + 1, "DOT-02", 2, now));
        await db.SaveChangesAsync();
        var version = await db.TblContractVersions.SingleAsync(
            x => x.VersionId == VersionId);
        version.SnapshotHash = new string('b', 64);
        version.IsLocked = true;
        version.LockedDate = now;
        version.LockedByEmployeeId = EmployeeId;
        db.TblContractVersionLegalSnapshots.Add(legalSnapshot);
        await db.SaveChangesAsync();
    }

    private static byte[] CreateTemplateDocx()
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(
            stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new W.Document(new W.Body(
                new W.Paragraph(new W.Run(new W.Text("BIÊN BẢN {{ACCEPTANCE_CODE}}"))),
                new W.Paragraph(new W.Run(new W.Text("{{DOCUMENT_REFERENCES}}"))),
                new W.Paragraph(new W.Run(new W.Text("{{ACCEPTANCE_SECTIONS}}")))));
            main.Document.Save();
        }
        return stream.ToArray();
    }

    private static byte[] CreateTemplateDocxWithCustomAndSignatures()
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(
                   stream,
                   DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new W.Document(new W.Body(
                FormattedParagraph("Liên hệ: {{ACCEPTANCE_CONTACT}}"),
                FormattedParagraph("{{SIGNATURE_CUSTOMER}}", centered: true),
                FormattedParagraph("{{SIGNATURE_PROVIDER}}", centered: true)));
            main.Document.Save();
        }
        return stream.ToArray();
    }

    private static W.Paragraph FormattedParagraph(
        string text,
        bool centered = false)
    {
        var paragraph = new W.Paragraph(new W.Run(
            new W.RunProperties(
                new W.RunFonts
                {
                    Ascii = "Arial",
                    HighAnsi = "Arial",
                    EastAsia = "Arial"
                },
                new W.FontSize { Val = "22" }),
            new W.Text(text)));
        if (centered)
        {
            paragraph.ParagraphProperties = new W.ParagraphProperties(
                new W.Justification { Val = W.JustificationValues.Center });
        }
        return paragraph;
    }

    private static TblContractPaymentMilestone Milestone(
        int id, string code, int order, DateTime now) => new()
    {
        PaymentMilestoneId = id,
        ContractId = ContractId,
        VersionId = VersionId,
        TermId = 1,
        MilestoneCode = code,
        TitleVi = "Thanh toán sau nghiệm thu",
        PaymentPercent = 50,
        DueAnchor = (byte)PaymentDueAnchor.AcceptanceCompleted,
        DueOffsetDays = 5,
        DayCountMode = (byte)PaymentDayCountMode.CalendarDays,
        DisplayOrder = order,
        Amount = 500000,
        PaymentStatus = (byte)ContractPaymentMilestoneStatus.Unpaid,
        CreatedEmployeeId = EmployeeId,
        CreatedDate = now,
        RowVersion = InitialVersion.ToArray()
    };

    private static FormFile FormFile(
        string name, string contentType, string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        return new FormFile(new MemoryStream(bytes), 0, bytes.Length,
            "evidenceFile", name) { Headers = new HeaderDictionary(), ContentType = contentType };
    }

    private static string Sha(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private sealed class TestAuthorization(bool denyManage = false)
        : IContractResourceAuthorizationService
    {
        public Task EnsureCanReadAsync(int contractId, int employeeId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureCanWriteAsync(int contractId, int employeeId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureCanManageSigningAsync(int contractId, int employeeId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureCanManageAcceptanceAsync(int contractId, int employeeId,
            CancellationToken cancellationToken = default) => denyManage
                ? Task.FromException(new UnauthorizedAccessException("denied"))
                : Task.CompletedTask;
        public Task EnsureCanManagePaymentAsync(int contractId, int employeeId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task EnsureCanCompleteAsync(int contractId, int employeeId,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class CapturingAuditWriter : IContractAuditWriter
    {
        public List<EmployeeContractAuditWriteRequest> EmployeeRequests { get; } = [];
        public void StageAudits(IReadOnlyCollection<ContractAuditWriteRequest> requests) { }
        public void StageEmployeeAudits(
            IReadOnlyCollection<EmployeeContractAuditWriteRequest> requests) =>
            EmployeeRequests.AddRange(requests);
    }

    private sealed class FakePdfRenderer : IContractTemplatePdfRenderer
    {
        public Task<byte[]> ConvertPreviewToPdfAsync(byte[] previewDocx,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(System.Text.Encoding.ASCII.GetBytes("%PDF-phase7"));
    }

    private sealed class CalendarDueDateService : IContractPaymentDueDateService
    {
        public DateTime Calculate(DateTime anchorDate, int offsetDays,
            PaymentDayCountMode dayCountMode) => anchorDate.AddDays(offsetDays);
    }

    private sealed class TrackingStorage : IPrivateFileStorage
    {
        private readonly Dictionary<string, byte[]> _content = [];
        private int _saveCount;
        public int? FailOnSaveNumber { get; set; }
        public List<string> SavedKeys { get; } = [];
        public List<string> DeletedKeys { get; } = [];

        public void Add(string key, byte[] content) => _content[key] = content;

        public async Task<StoredPrivateFile> SaveAsync(PrivateFileSaveRequest request,
            CancellationToken cancellationToken = default)
        {
            _saveCount++;
            if (FailOnSaveNumber == _saveCount)
                throw new IOException("simulated storage failure");
            using var memory = new MemoryStream();
            await request.Content.CopyToAsync(memory, cancellationToken);
            var bytes = memory.ToArray();
            var key = $"phase7/{Guid.NewGuid():N}/{request.OriginalFileName}";
            _content[key] = bytes;
            SavedKeys.Add(key);
            return new(key, request.OriginalFileName, request.ContentType,
                bytes.LongLength, Sha(bytes), DateTime.UtcNow,
                request.TenantCode);
        }

        public Task<Stream> OpenReadAsync(string tenantCode, string storageKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream>(new MemoryStream(_content[storageKey], false));

        public Task DeleteAsync(string tenantCode, string storageKey,
            CancellationToken cancellationToken = default)
        {
            _content.Remove(storageKey);
            DeletedKeys.Add(storageKey);
            return Task.CompletedTask;
        }
    }
}
