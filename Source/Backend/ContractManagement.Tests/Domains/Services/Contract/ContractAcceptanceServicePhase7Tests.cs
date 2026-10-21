using System.Security.Cryptography;
using ContractManagement.API.Common.Enums;
using ContractManagement.API.Common.Exceptions;
using ContractManagement.API.Domains.DTOs.Requests.Contract;
using ContractManagement.API.Domains.Notifications;
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

public sealed partial class ContractAcceptanceServicePhase7Tests
{
    [Fact]
    public async Task Preview_MissingCustomerEnglishCustomSource_RendersBlankFromSignedSnapshot()
    {
        await using var db = CreateContext();
        var storage = new TrackingStorage();
        await SeedAsync(db, storage);
        (await db.TblCustomers.SingleAsync()).CustomerBankName = "Không dùng ngân hàng VI";
        db.TblContractTemplateFields.Add(new()
        {
            TemplateVersionId = TemplateVersionId, PlaceholderKey = "ACCEPTANCE_CONTACT",
            FieldLabel = "Ngân hàng tiếng Anh", DataSource = "Customer", SourceFieldKey = "counterparty.bank-name-en",
            DataKind = 1, Multiplicity = 2, IsSystem = false, DefaultValue = "Không dùng mặc định"
        });
        await db.SaveChangesAsync();
        storage.Add("templates/acceptance.docx", CreateTemplateDocxWithCustomAndSignatures());
        var service = CreateService(db, storage);
        var draft = await service.CreateAsync(ContractId, CreateRequest(), EmployeeId);
        var preview = await service.PreviewAsync(ContractId, draft.AcceptanceRecordId, "docx", EmployeeId);
        using var doc = WordprocessingDocument.Open(new MemoryStream(preview.Content), false);
        var text = doc.MainDocumentPart!.Document!.InnerText;
        Assert.DoesNotContain("{{ACCEPTANCE_CONTACT}}", text);
        Assert.DoesNotContain("Không dùng ngân hàng VI", text);
        Assert.DoesNotContain("Không dùng mặc định", text);
    }

    [Fact]
    public async Task Create_EnglishContractReference_DoesNotRequireOptionalCustomerLegalName()
    {
        await using var db = CreateContext();
        var storage = new TrackingStorage();
        await SeedAsync(db, storage, contractNameEn: "Software supply contract", tenantLegalNameEn: "Provider company");
        var draft = await CreateService(db, storage).CreateAsync(ContractId, CreateRequest(), EmployeeId);
        var reference = Assert.Single(draft.References, x => x.ReferenceType == AcceptanceReferenceType.Contract);
        Assert.NotNull(reference.TextEn);
        Assert.Contains("Provider company and  regarding Software supply contract", reference.TextEn);
        Assert.DoesNotContain("Khách hàng A", reference.TextEn);
        Assert.Contains("12 September 2026", reference.TextEn);
    }

    private const int ContractId = 71;
    private const int VersionId = 72;
    private const int TemplateId = 73;
    private const int TemplateVersionId = 74;
    private const int EmployeeId = 75;
    private const int AppendixId = 76;
    private const int MilestoneId = 77;
    private static readonly byte[] InitialVersion = [1, 2, 3, 4, 5, 6, 7, 8];

    [Fact]
    public async Task FinalizeAndSign_NotifyOwnerOnly_WhenAnotherEmployeeActs()
    {
        await using var db = CreateContext();
        var storage = new TrackingStorage();
        await SeedAsync(db, storage);
        const int actorId = 750;
        db.TblEmployees.Add(new TblEmployee
        {
            EmployeeId = actorId, EmployeeFullName = "Nhân viên nghiệm thu",
            Status = 1, RowVersion = InitialVersion.ToArray()
        });
        await db.SaveChangesAsync();
        var authorization = new TestAuthorization();
        var notifications = new ContractNotificationProducer(db,
            new NotificationPublisher(db,
                [new ContractNotificationResourcePolicy(db, authorization)]));
        var service = CreateService(db, storage, authorization: authorization,
            notifications: notifications);

        var draft = await service.CreateAsync(ContractId, CreateRequest(), actorId);
        draft = await SelectFixtureHandoverScopeAsync(db, storage, service, draft);
        var finalized = await service.FinalizeAsync(ContractId,
            draft.AcceptanceRecordId,
            new() { RowVersion = draft.RowVersion }, actorId);
        await service.UploadSignedEvidenceAsync(ContractId, draft.AcceptanceRecordId,
            new()
            {
                RowVersion = finalized.RowVersion,
                SignDate = new DateTime(2026, 9, 20),
                File = FormFile("signed.pdf", "application/pdf", "%PDF-signed")
            }, actorId);

        var notificationsSaved = await db.TblNotifications.AsNoTracking()
            .Include(x => x.Recipients).OrderBy(x => x.NotificationId).ToListAsync();
        Assert.Equal(["AcceptanceFinalized", "AcceptanceSigned"],
            notificationsSaved.Select(x => x.EventCode).ToArray());
        Assert.All(notificationsSaved, item =>
            Assert.Equal(EmployeeId, Assert.Single(item.Recipients).EmployeeId));
        Assert.All(notificationsSaved, item =>
            Assert.DoesNotContain(item.Recipients, recipient => recipient.EmployeeId == actorId));
    }

    [Theory]
    [InlineData(ContractDirection.Sale)]
    [InlineData(ContractDirection.Purchase)]
    public async Task Lifecycle_UsesRelationalSnapshot_Artifacts_AndSignedGate(ContractDirection direction)
    {
        await using var db = CreateContext();
        var storage = new TrackingStorage();
        await SeedAsync(db, storage, direction: direction);
        var audit = new CapturingAuditWriter();
        var service = CreateService(db, storage, audit);

        var draft = await service.CreateAsync(
            ContractId, CreateRequest(), EmployeeId);
        draft = await SelectFixtureHandoverScopeAsync(db, storage, service, draft);

        Assert.Equal(AcceptanceRecordStatus.Draft, draft.Status);
        Assert.Equal(3, draft.References.Count);
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
        Assert.Equal(EmployeeId, finalized.FinalizedByEmployeeId);
        Assert.Equal("Nguyễn Văn B", finalized.FinalizedByEmployeeName);
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
                SignDate = new DateTime(2026, 9, 20),
                File = FormFile("signed.pdf", "application/pdf", "%PDF-signed")
            }, EmployeeId);
        var signed = await service.GetAsync(
            ContractId, draft.AcceptanceRecordId, EmployeeId);

        Assert.Equal(AcceptanceRecordStatus.Signed, signed.Status);
        Assert.Equal(evidence.AcceptanceEvidenceId,
            signed.SignedEvidence!.AcceptanceEvidenceId);
        var milestone = await db.TblContractPaymentMilestones
            .SingleAsync(x => x.PaymentMilestoneId == MilestoneId);
        Assert.Equal(new DateTime(2026, 9, 20), evidence.SignDate);
        Assert.Equal(evidence.SignDate, milestone.AnchorDate);
        Assert.Equal(evidence.UploadedAt, signed.SignedAt);
        Assert.NotEqual(evidence.SignDate, evidence.UploadedAt.Date);
        Assert.Equal(evidence.SignDate, signed.SignedEvidence.SignDate);
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
    public async Task Preview_EnglishCustomSourceUsesSignedSnapshot_NotCurrentCustomer()
    {
        await using var db = CreateContext();
        var storage = new TrackingStorage();
        await SeedAsync(db, storage, customerBankNameEn: "Frozen English bank");
        (await db.TblCustomers.SingleAsync()).CustomerBankNameEn = "Current English bank";
        db.TblContractTemplateFields.Add(new()
        {
            TemplateVersionId = TemplateVersionId, PlaceholderKey = "ACCEPTANCE_CONTACT",
            FieldLabel = "Ngân hàng tiếng Anh", DataSource = "Customer", SourceFieldKey = "counterparty.bank-name-en",
            DataKind = 1, Multiplicity = 2, IsSystem = false
        });
        await db.SaveChangesAsync();
        storage.Add("templates/acceptance.docx", CreateTemplateDocxWithCustomAndSignatures());
        var service = CreateService(db, storage);
        var draft = await service.CreateAsync(ContractId, CreateRequest(), EmployeeId);
        var preview = await service.PreviewAsync(ContractId, draft.AcceptanceRecordId, "docx", EmployeeId);
        using var doc = WordprocessingDocument.Open(new MemoryStream(preview.Content), false);
        Assert.Contains("Frozen English bank", doc.MainDocumentPart!.Document!.InnerText);
        Assert.DoesNotContain("Current English bank", doc.MainDocumentPart.Document.InnerText);
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
            SourceFieldKey = "counterparty.contact-name",
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
        Assert.Contains("ĐẠI DIỆN BÊN KHÁCH HÀNG", text);
        var customerSignature = document.MainDocumentPart.Document.Body!
            .Elements<W.Paragraph>()
            .Single(paragraph => paragraph.InnerText ==
                "ĐẠI DIỆN BÊN KHÁCH HÀNG");
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
        Assert.Contains("ĐẠI DIỆN BÊN CUNG CẤP", text);
        Assert.DoesNotContain("Nguyễn Văn B", text);
        Assert.DoesNotContain("(Chữ ký)", text);
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

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
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

    [Theory]
    [InlineData(AcceptanceRecordStatus.Draft)]
    [InlineData(AcceptanceRecordStatus.Finalized)]
    public async Task Create_RejectsWhenAnotherRecordIsInProgress(
        AcceptanceRecordStatus status)
    {
        await using var db = CreateContext();
        var storage = new TrackingStorage();
        await SeedAsync(db, storage);
        db.TblContractAcceptanceRecords.Add(
            AcceptanceRecord(900, status, AcceptanceKind.Partial));
        await db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateService(db, storage).CreateAsync(
                ContractId, CreateRequest(), EmployeeId));

        Assert.Equal("AcceptanceRecordInProgress", exception.Code);
    }

    [Fact]
    public async Task Create_RejectsAfterSignedFinalAcceptance()
    {
        await using var db = CreateContext();
        var storage = new TrackingStorage();
        await SeedAsync(db, storage);
        db.TblContractAcceptanceRecords.Add(AcceptanceRecord(
            900, AcceptanceRecordStatus.Signed, AcceptanceKind.Final));
        await db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateService(db, storage).CreateAsync(
                ContractId, CreateRequest(), EmployeeId));

        Assert.Equal("AcceptanceFinalAlreadySigned", exception.Code);
    }

    [Fact]
    public async Task Create_RejectsDuplicateCodeEvenWhenPriorRecordWasCancelled()
    {
        await using var db = CreateContext();
        var storage = new TrackingStorage();
        await SeedAsync(db, storage);
        db.TblContractAcceptanceRecords.Add(AcceptanceRecord(
            900, AcceptanceRecordStatus.Cancelled, AcceptanceKind.Partial));
        await db.SaveChangesAsync();
        var request = CreateRequest();
        request.AcceptanceCode = " bbnt-900 ";

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateService(db, storage).CreateAsync(
                ContractId, request, EmployeeId));

        Assert.Equal("AcceptanceCodeAlreadyExists", exception.Code);
        Assert.Equal(StatusCodes.Status409Conflict, exception.StatusCode);
        Assert.Contains("Mã biên bản đã tồn tại", exception.Message);
    }

    [Fact]
    public async Task Update_RejectsDuplicateCode()
    {
        await using var db = CreateContext();
        var storage = new TrackingStorage();
        await SeedAsync(db, storage);
        var service = CreateService(db, storage);
        var draft = await service.CreateAsync(
            ContractId, CreateRequest(), EmployeeId);
        db.TblContractAcceptanceRecords.Add(AcceptanceRecord(
            900, AcceptanceRecordStatus.Cancelled, AcceptanceKind.Partial));
        await db.SaveChangesAsync();
        var customer = draft.Parties.Single(party =>
            party.PartyRole == AcceptancePartyRole.Customer);
        var tenant = draft.Parties.Single(party =>
            party.PartyRole == AcceptancePartyRole.Tenant);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.UpdateAsync(ContractId, draft.AcceptanceRecordId,
                new UpdateContractAcceptanceRecordRequest
                {
                    AcceptanceCode = " bbnt-900 ",
                    AcceptanceDate = draft.AcceptanceDate,
                    Location = draft.Location,
                    AcceptanceKind = draft.AcceptanceKind,
                    CustomerParty = new ContractAcceptancePartyInput
                    {
                        LegalName = customer.LegalName,
                        RepresentativeName = customer.RepresentativeName
                    },
                    TenantParty = new ContractAcceptancePartyInput
                    {
                        LegalName = tenant.LegalName,
                        RepresentativeName = tenant.RepresentativeName
                    },
                    RowVersion = draft.RowVersion
                }, EmployeeId));

        Assert.Equal("AcceptanceCodeAlreadyExists", exception.Code);
        Assert.Equal(StatusCodes.Status409Conflict, exception.StatusCode);
    }

    [Fact]
    public async Task Create_AfterSignedPartial_AllowsOnlyRemainingMilestone()
    {
        await using var db = CreateContext();
        var storage = new TrackingStorage();
        await SeedAsync(db, storage);
        db.TblContractAcceptanceRecords.Add(AcceptanceRecord(
            900, AcceptanceRecordStatus.Signed, AcceptanceKind.Partial));
        db.TblContractAcceptanceMilestones.Add(
            new TblContractAcceptanceMilestone
            {
                AcceptanceRecordId = 900,
                PaymentMilestoneId = MilestoneId
            });
        await db.SaveChangesAsync();
        var service = CreateService(db, storage);

        var reusedException = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.CreateAsync(
                ContractId, CreateRequest(), EmployeeId));
        Assert.Equal("AcceptanceMilestoneAlreadyFinalized",
            reusedException.Code);

        var request = CreateRequest();
        request.AcceptanceCode = "BBNT-002";
        request.PaymentMilestoneIds = [MilestoneId + 1];
        var draft = await service.CreateAsync(
            ContractId, request, EmployeeId);

        Assert.Equal(AcceptanceRecordStatus.Draft, draft.Status);
        Assert.Equal([MilestoneId + 1],
            draft.Milestones.Select(x => x.PaymentMilestoneId));
        var summaries = await service.ListAsync(ContractId, EmployeeId);
        Assert.Contains(summaries, x => x.AcceptanceRecordId == 900
            && x.PaymentMilestoneIds.SequenceEqual([MilestoneId]));
    }

    [Fact]
    public async Task Create_FinalAfterCancelledPartial_RemainsLockedToPartialMode()
    {
        await using var db = CreateContext();
        var storage = new TrackingStorage();
        await SeedAsync(db, storage);
        db.TblContractAcceptanceRecords.Add(AcceptanceRecord(
            900, AcceptanceRecordStatus.Cancelled, AcceptanceKind.Partial));
        await db.SaveChangesAsync();
        var request = CreateRequest();
        request.AcceptanceKind = AcceptanceKind.Final;
        request.PaymentMilestoneIds = [];

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateService(db, storage).CreateAsync(
                ContractId, request, EmployeeId));

        Assert.Equal("AcceptancePartialFlowLocked", exception.Code);
        Assert.Equal(StatusCodes.Status409Conflict, exception.StatusCode);
    }

    [Fact]
    public async Task FinalBeforeAnyPartial_CanBeSignedAndBlocksFurtherRecords()
    {
        await using var db = CreateContext();
        var storage = new TrackingStorage();
        await SeedAsync(db, storage);
        var service = CreateService(db, storage);
        var request = CreateRequest();
        request.AcceptanceKind = AcceptanceKind.Final;
        request.PaymentMilestoneIds = [];

        var draft = await service.CreateAsync(
            ContractId, request, EmployeeId);
        draft = await SelectFixtureHandoverScopeAsync(db, storage, service, draft);
        var finalized = await service.FinalizeAsync(ContractId,
            draft.AcceptanceRecordId,
            new() { RowVersion = draft.RowVersion }, EmployeeId);
        await service.UploadSignedEvidenceAsync(ContractId,
            finalized.AcceptanceRecordId, new()
            {
                RowVersion = finalized.RowVersion,
                SignDate = new DateTime(2026, 9, 20),
                File = FormFile("signed.pdf", "application/pdf", "%PDF-signed")
            }, EmployeeId);

        var next = CreateRequest();
        next.AcceptanceCode = "BBNT-002";
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(ContractId, next, EmployeeId));

        Assert.Equal("AcceptanceFinalAlreadySigned", exception.Code);
    }

    [Fact]
    public async Task Update_PartialDraftCannotBecomeFinal()
    {
        await using var db = CreateContext();
        var storage = new TrackingStorage();
        await SeedAsync(db, storage);
        var service = CreateService(db, storage);
        var draft = await service.CreateAsync(
            ContractId, CreateRequest(), EmployeeId);
        var customer = draft.Parties.Single(x =>
            x.PartyRole == AcceptancePartyRole.Customer);
        var tenant = draft.Parties.Single(x =>
            x.PartyRole == AcceptancePartyRole.Tenant);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.UpdateAsync(ContractId, draft.AcceptanceRecordId,
                new UpdateContractAcceptanceRecordRequest
                {
                    AcceptanceCode = draft.AcceptanceCode,
                    AcceptanceDate = draft.AcceptanceDate,
                    Location = draft.Location,
                    AcceptanceKind = AcceptanceKind.Final,
                    CustomerParty = new ContractAcceptancePartyInput
                    {
                        LegalName = customer.LegalName,
                        RepresentativeName = customer.RepresentativeName
                    },
                    TenantParty = new ContractAcceptancePartyInput
                    {
                        LegalName = tenant.LegalName,
                        RepresentativeName = tenant.RepresentativeName
                    },
                    RowVersion = draft.RowVersion
                }, EmployeeId));

        Assert.Equal("AcceptancePartialFlowLocked", exception.Code);
        Assert.Equal(AcceptanceKind.Partial,
            (await service.GetAsync(ContractId, draft.AcceptanceRecordId,
                EmployeeId)).AcceptanceKind);
    }

    [Fact]
    public async Task DraftMutations_RejectWhenCurrentVersionChanges()
    {
        await using var db = CreateContext();
        var storage = new TrackingStorage();
        await SeedAsync(db, storage);
        var service = CreateService(db, storage);
        var draft = await service.CreateAsync(
            ContractId, CreateRequest(), EmployeeId);
        var contract = await db.TblContracts.SingleAsync();
        contract.CurrentVersionId = VersionId + 1;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var update = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.UpdateAsync(ContractId, draft.AcceptanceRecordId,
                new UpdateContractAcceptanceRecordRequest
                {
                    AcceptanceCode = draft.AcceptanceCode,
                    AcceptanceDate = draft.AcceptanceDate,
                    Location = draft.Location,
                    AcceptanceKind = draft.AcceptanceKind,
                    CustomerParty = new ContractAcceptancePartyInput(),
                    TenantParty = new ContractAcceptancePartyInput(),
                    RowVersion = draft.RowVersion
                }, EmployeeId));
        var milestones = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ReplaceMilestonesAsync(ContractId,
                draft.AcceptanceRecordId, new()
                {
                    RowVersion = draft.RowVersion,
                    PaymentMilestoneIds = [MilestoneId]
                }, EmployeeId));

        Assert.Equal("AcceptanceSourceChanged", update.Code);
        Assert.Equal("AcceptanceSourceChanged", milestones.Code);
    }

    [Fact]
    public async Task Create_WhenSignedPartialsCoverAllMilestones_StillAllowsRemainingScope()
    {
        await using var db = CreateContext();
        var storage = new TrackingStorage();
        await SeedAsync(db, storage);
        foreach (var index in new[] { 0, 1 })
        {
            var recordId = 900 + index;
            var fileId = 910 + index;
            db.TblContractAcceptanceRecords.Add(AcceptanceRecord(
                recordId, AcceptanceRecordStatus.Signed,
                AcceptanceKind.Partial));
            db.TblContractAcceptanceMilestones.Add(new()
            {
                AcceptanceRecordId = recordId,
                PaymentMilestoneId = MilestoneId + index
            });
            db.TblFileStorages.Add(new TblFileStorage
            {
                FileId = fileId,
                ObjectType = "ContractAcceptanceEvidence",
                ObjectId = ContractId,
                FileName = $"signed-{index}.pdf",
                FilePath = string.Empty,
                StorageKey = $"acceptance/signed-{index}.pdf",
                TenantCode = "TENANT-PHASE7",
                ContentType = "application/pdf",
                FileType = "pdf",
                FileSize = 12,
                Sha256 = new string('a', 64)
            });
            db.TblContractAcceptanceEvidences.Add(new()
            {
                AcceptanceEvidenceId = 920 + index,
                AcceptanceRecordId = recordId,
                FileId = fileId,
                UploadedByEmployeeId = EmployeeId,
                UploadedAt = DateTime.UtcNow,
                RowVersion = InitialVersion.ToArray()
            });
        }
        await db.SaveChangesAsync();
        var request = CreateRequest();
        request.AcceptanceCode = "BBNT-NEW";
        request.PaymentMilestoneIds = [];

        var next = await CreateService(db, storage).CreateAsync(ContractId, request, EmployeeId);
        Assert.Equal(AcceptanceKind.Partial, next.AcceptanceKind);
        Assert.Empty(next.Milestones);
        Assert.NotNull(next.HandoverBlocker);
    }

    [Fact]
    public async Task Finalize_RechecksScopeAndCleansArtifactsOnOverlap()
    {
        await using var db = CreateContext();
        var storage = new TrackingStorage();
        await SeedAsync(db, storage);
        var service = CreateService(db, storage);
        var draft = await service.CreateAsync(
            ContractId, CreateRequest(), EmployeeId);
        draft = await SelectFixtureHandoverScopeAsync(db, storage, service, draft);
        db.TblContractAcceptanceRecords.Add(AcceptanceRecord(
            900, AcceptanceRecordStatus.Signed, AcceptanceKind.Partial));
        db.TblContractAcceptanceMilestones.Add(new()
        {
            AcceptanceRecordId = 900,
            PaymentMilestoneId = MilestoneId
        });
        await db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.FinalizeAsync(ContractId, draft.AcceptanceRecordId,
                new() { RowVersion = draft.RowVersion }, EmployeeId));

        Assert.Equal("AcceptanceMilestoneAlreadyFinalized", exception.Code);
        Assert.Equal((byte)AcceptanceRecordStatus.Draft,
            (await db.TblContractAcceptanceRecords.AsNoTracking()
                .SingleAsync(x => x.AcceptanceRecordId ==
                    draft.AcceptanceRecordId)).Status);
        Assert.DoesNotContain(db.TblFileStorages,
            x => x.ObjectType == "ContractAcceptanceArtifact");
        Assert.Empty(storage.DeletedKeys);
    }

    [Fact]
    public async Task SignedEvidence_RechecksScopeAndDeletesUploadedFileOnOverlap()
    {
        await using var db = CreateContext();
        var storage = new TrackingStorage();
        await SeedAsync(db, storage);
        var service = CreateService(db, storage);
        var draft = await service.CreateAsync(
            ContractId, CreateRequest(), EmployeeId);
        draft = await SelectFixtureHandoverScopeAsync(db, storage, service, draft);
        var finalized = await service.FinalizeAsync(
            ContractId, draft.AcceptanceRecordId,
            new() { RowVersion = draft.RowVersion }, EmployeeId);
        db.TblContractAcceptanceRecords.Add(AcceptanceRecord(
            900, AcceptanceRecordStatus.Signed, AcceptanceKind.Partial));
        db.TblContractAcceptanceMilestones.Add(new()
        {
            AcceptanceRecordId = 900,
            PaymentMilestoneId = MilestoneId
        });
        await db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.UploadSignedEvidenceAsync(ContractId,
                finalized.AcceptanceRecordId, new()
                {
                    RowVersion = finalized.RowVersion,
                    SignDate = new DateTime(2026, 9, 20),
                    File = FormFile("signed.pdf", "application/pdf", "%PDF-signed")
                }, EmployeeId));

        Assert.Equal("AcceptanceMilestoneAlreadyFinalized", exception.Code);
        Assert.Empty(db.TblContractAcceptanceEvidences);
        Assert.Single(storage.DeletedKeys);
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
        draft = await SelectFixtureHandoverScopeAsync(db, storage, service, draft);
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
        draft = await SelectFixtureHandoverScopeAsync(db, storage, service, draft);
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
        draft = await SelectFixtureHandoverScopeAsync(db, storage, service, draft);
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
                    SignDate = new DateTime(2026, 9, 20),
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
        IContractAuditWriter? audit = null,
        IContractResourceAuthorizationService? authorization = null,
        ContractNotificationProducer? notifications = null)
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
            new CalendarDueDateService(),
            notifications: notifications);
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

    private static TblContractAcceptanceRecord AcceptanceRecord(
        int id, AcceptanceRecordStatus status, AcceptanceKind kind) => new()
    {
        AcceptanceRecordId = id,
        ContractId = ContractId,
        ContractVersionId = VersionId,
        TemplateVersionId = TemplateVersionId,
        AcceptanceCode = $"BBNT-{id}",
        AcceptanceDate = new DateTime(2026, 9, 18),
        Location = "Hà Nội",
        AcceptanceKind = (byte)kind,
        Status = (byte)status,
        CreatedEmployeeId = EmployeeId,
        CreatedDate = new DateTime(2026, 9, 18, 8, 0, 0,
            DateTimeKind.Utc),
        RowVersion = InitialVersion.ToArray()
    };

    private static async Task SeedAsync(
        DbDtctechContext db, TrackingStorage storage, string? customerBankNameEn = null,
        string? contractNameEn = null, string? tenantLegalNameEn = null,
        ContractDirection direction = ContractDirection.Sale, string? counterpartyBank = null,
        ContractLanguageMode language = ContractLanguageMode.Vietnamese,
        TemplateDocumentType documentType = TemplateDocumentType.AcceptanceRecord, byte[]? documentBytes = null,
        IReadOnlyList<TblContractItem>? handoverItems = null, bool omitMilestones = false, decimal contractTotal = 0m,
        string? counterpartyContactPhone = null, bool omitSections = false)
    {
        var now = new DateTime(2026, 9, 18, 8, 0, 0, DateTimeKind.Utc);
        var templateDocx = documentBytes ?? CreateTemplateDocx();
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
                ObjectType = documentType == TemplateDocumentType.HandoverRecord ? "ContractTemplateVersion" : "ContractTemplateDocument",
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
            DocumentType = (byte)documentType,
            LanguageMode = (byte)language,
            CurrentPublishedVersionId = TemplateVersionId,
            IsActive = true,
            CreatedEmployeeId = EmployeeId,
            CreatedDate = now,
            RowVersion = InitialVersion.ToArray()
        });
        db.TblContractTemplateVersions.Add(new TblContractTemplateVersion
        {
            ContractDirection = (byte)direction,
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
        if (!omitSections) db.TblContractTemplateTerms.Add(new TblContractTemplateTerm
        {
            TemplateTermId = 1,
            TemplateVersionId = TemplateVersionId,
            TermCode = "RESULT",
            TermTitle = "I. KẾT QUẢ THỰC HIỆN",
            TermTitleEn = documentType == TemplateDocumentType.HandoverRecord ? "Handover results" : null,
            TermContentEn = documentType == TemplateDocumentType.HandoverRecord ? RichText("Delivered scope") : null,
            TermContent = RichText("Đã hoàn thành công việc."),
            DisplayOrder = 1,
            CreatedEmployeeId = EmployeeId,
            CreatedDate = now,
            RowVersion = InitialVersion.ToArray()
        });
        db.TblContracts.Add(new TblContract
        {
            ContractDirection = (byte)direction,
            ContractId = ContractId,
            CustomerId = 1,
            EmployeeId = EmployeeId,
            ContractType = (byte)ContractType.SoftwareSupply,
            TemplateVersionId = 1,
            CurrentVersionId = VersionId,
            TotalAmount = contractTotal,
            ContractCode = "HD-001",
            ContractName = "Cung cấp phần mềm",
            SignDate = new DateTime(2026, 9, 12),
            Status = (byte)ContractStatus.Signed,
            CurrencyCode = "VND",
            LanguageMode = (byte)language,
            CreatedEmployeeId = EmployeeId,
            CreatedDate = now,
            RowVersion = InitialVersion.ToArray()
        });
        db.TblContractVersions.Add(new TblContractVersion
        {
            VersionId = VersionId,
            ContractId = ContractId,
            VersionNo = 1,
            TotalAmount = contractTotal,
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
            ContractDirection = (byte)direction,
            ContractVersionLegalSnapshotId = 1,
            VersionId = VersionId,
            ContractId = ContractId,
            VersionNo = 1,
            TemplateVersionId = 1,
            ContractCode = "HD-001",
            ContractName = "Cung cấp phần mềm",
            ContractNameEn = contractNameEn,
            ContractType = (byte)ContractType.SoftwareSupply,
            ContractCreatedDate = now,
            SignDate = new DateTime(2026, 9, 12),
            CurrencyCode = "VND",
            LanguageMode = (byte)language,
            CreatedDate = now,
            CreatedByEmployeeId = EmployeeId,
            Parties =
            [
                new TblContractVersionPartySnapshot
                {
                    ContractVersionPartySnapshotId = 1,
                    PartyRole = 1,
                    LegalName = "Công ty DTC",
                    LegalNameEn = tenantLegalNameEn,
                    Address = "Hà Nội",
                    RepresentativeName = "Nguyễn Văn B",
                    RepresentativeTitle = "Giám đốc"
                },
                new TblContractVersionPartySnapshot
                {
                    ContractVersionPartySnapshotId = 2,
                    PartyRole = 2,
                    SourceCustomerId = 1,
                    ContactName = "Người liên hệ nghiệm thu",
                    ContactTitle = "Điều phối dự án",
                    ContactPhone = counterpartyContactPhone,
                    LegalName = "Khách hàng A",
                    BankName = counterpartyBank,
                    BankNameEn = customerBankNameEn,
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
        if (handoverItems is not null) db.TblContractItems.AddRange(handoverItems);
        using (var source = WordprocessingDocument.Open(new MemoryStream(templateDocx), false))
        {
            var text = source.MainDocumentPart!.Document!.InnerText;
            var definitions = documentType == TemplateDocumentType.HandoverRecord
                ? HandoverRecordPlaceholderCatalog.All
                : AcceptanceRecordPlaceholderCatalog.All.Where(x => x.Key == "ACCEPTANCE_SECTIONS").ToArray();
            db.TblContractTemplateFields.AddRange(definitions.Where(x => text.Contains("{{" + x.Key + "}}", StringComparison.Ordinal)).Select((x, i) => new TblContractTemplateField {
                TemplateVersionId = TemplateVersionId, PlaceholderKey = x.Key, FieldLabel = x.Label, SourceFieldKey = x.SourceFieldKey!, DataSource = x.DataSource,
                DataKind = (byte)x.DataKind, Multiplicity = (byte)x.Multiplicity, IsSystem = true, IsRequired = false, DisplayOrder = i, CreatedEmployeeId = EmployeeId, CreatedDate = now }));
        }
        if (!omitMilestones) db.TblContractPaymentMilestones.AddRange(
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
