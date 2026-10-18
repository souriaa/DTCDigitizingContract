using ContractManagement.API.Common.Enums;
using ContractManagement.API.Domains.DTOs.Requests.Contract;
using ContractManagement.Infrastructure.Persistence.Application;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Tests.Domains.Services.Contract;

public sealed partial class ContractServiceSlice04Tests
{
    [Theory]
    [InlineData("{\"termKind\":0}")]
    [InlineData("{\"paymentMilestones\":[]}")]
    public void AuthoringPayload_MustExplicitlyIncludeKindAndPaymentPlan(string json)
    {
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        Assert.Throws<System.Text.Json.JsonException>(() => System.Text.Json.JsonSerializer.Deserialize<CreateContractTermRequest>(json, options));
        Assert.Throws<System.Text.Json.JsonException>(() => System.Text.Json.JsonSerializer.Deserialize<UpdateContractTermRequest>(json, options));
    }

    [Theory]
    [InlineData(1, 0, 0, 1)]
    [InlineData(2, 1, 1, 0)]
    [InlineData(3, 1, 1, 1)]
    public async Task Create_PaymentAmountsKeepPerMilestoneRoundingAndBalanceLast(int total, int first, int second, int last)
    {
        await using var db = CreateContext(); await SeedCreateDependenciesAsync(db);
        var request = CreateRequest("VND", [CreateItem(ContractItemType.Product, 1, total, sourceProductId: ProductId)]);
        request.Terms = [new() { TermCode = "PAYMENT", TermTitle = "Thanh toán", TermKind = ContractTermKind.Payment, DisplayOrder = 1,
            PaymentMilestones = new decimal[] { 33.3333m, 33.3333m, 33.3334m }.Select((percent, i) => new ContractPaymentMilestoneDraftRequest {
                MilestoneCode = $"P{i + 1}", TitleVi = $"Đợt {i + 1}", PaymentPercent = percent,
                DueAnchor = PaymentDueAnchor.ContractSigned, DayCountMode = PaymentDayCountMode.CalendarDays }).ToList() }];
        await CreateService(db).CreateAsync(request, EmployeeId);
        Assert.Equal(new decimal[] { first, second, last }, await db.TblContractPaymentMilestones.OrderBy(x => x.DisplayOrder).Select(x => x.Amount).ToListAsync());
    }

    [Fact]
    public async Task Create_AllowsCustomPaymentTermWithoutChangingTemplate()
    {
        await using var db = CreateContext();
        await SeedCreateDependenciesAsync(db);
        var request = CreateRequest("VND", [CreateItem(ContractItemType.Product, 1, 101, sourceProductId: ProductId)]);
        request.Terms = [new CreateContractTermRequest {
            TermCode = "PAYMENT", TermTitle = "Thanh toán", TermKind = ContractTermKind.Payment,
            DisplayOrder = 1, PaymentMilestones = [
                new() { MilestoneCode = "P1", TitleVi = "Đợt 1", PaymentPercent = 33.3333m, DueAnchor = PaymentDueAnchor.ContractEffectiveDate,
                    DayCountMode = PaymentDayCountMode.CalendarDays, DueOffsetDays = 2 },
                new() { MilestoneCode = "P2", TitleVi = "Đợt 2", PaymentPercent = 66.6667m, DueAnchor = PaymentDueAnchor.ManualDate,
                    DayCountMode = PaymentDayCountMode.CalendarDays, AnchorDate = new DateTime(2026, 10, 18) }
            ] }];
        request.EffectiveDate = new DateTime(2026, 10, 1);
        await CreateService(db).CreateAsync(request, EmployeeId);
        var rows = await db.TblContractPaymentMilestones.OrderBy(x => x.DisplayOrder).ToListAsync();
        Assert.Equal(new decimal[] { 34, 67 }, rows.Select(x => x.Amount));
        Assert.Equal(new DateTime(2026, 10, 3), rows[0].DueDate);
        Assert.Equal(new DateTime(2026, 10, 18), rows[1].AnchorDate);
        Assert.All(rows, row => Assert.Null(row.SourceTemplatePaymentMilestoneId));
        Assert.Equal((byte)ContractTermKind.General, (await db.TblContractTemplateTerms.SingleAsync()).TermKind);
        Assert.Empty(db.TblContractTemplatePaymentMilestones);
    }
}

public partial class ContractServiceResponsibilityTransferTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    public async Task PaymentPlan_CanSaveIncompletePlanButCannotSubmit(decimal percent)
    {
        await using var db = CreateContext(); await SeedContractAsync(db);
        var request = CreateUpdateRequest();
        request.Terms[0].TermKind = ContractTermKind.Payment;
        request.Terms[0].PaymentMilestones = percent == 0 ? [] : [DraftMilestone("P1", percent)];
        var detail = await CreateService(db).UpdateDraftAsync(ContractId, request, CurrentResponsibleEmployeeId);
        Assert.Contains(detail.ApprovalReadiness.Blockers, x => x.Code == "PaymentPlanInvalid");
        Assert.False(detail.ApprovalReadiness.CanSubmit);
    }

    [Theory]
    [InlineData(ContractStatus.Draft)]
    [InlineData(ContractStatus.Negotiating)]
    public async Task PaymentPlan_CanAddEditReorderAndDeleteDuringAuthoring(ContractStatus status)
    {
        await using var db = CreateContext(); await SeedContractAsync(db);
        (await db.TblContracts.SingleAsync()).Status = (byte)status;
        await db.SaveChangesAsync();
        var request = CreateUpdateRequest();
        request.Terms.Add(new() { TermCode = "PAYMENT", TermTitle = "Thanh toán", TermKind = ContractTermKind.Payment, DisplayOrder = 2,
            PaymentMilestones = [DraftMilestone("P1", 50), DraftMilestone("P2", 50)] });
        await CreateService(db).UpdateDraftAsync(ContractId, request, CurrentResponsibleEmployeeId);
        await StampPaymentRowVersions(db);
        var ids = await db.TblContractPaymentMilestones.OrderBy(x => x.DisplayOrder).Select(x => x.PaymentMilestoneId).ToListAsync();
        request = await PaymentUpdateRequest(db);
        var payment = request.Terms.Single(x => x.TermKind == ContractTermKind.Payment);
        payment.PaymentMilestones.Reverse();
        payment.PaymentMilestones[0].PaymentPercent = 30;
        payment.PaymentMilestones[1].PaymentPercent = 70;
        payment.PaymentMilestones[1].DueAnchor = PaymentDueAnchor.ManualDate;
        payment.PaymentMilestones[1].AnchorDate = new DateTime(2026, 10, 18);
        await CreateService(db).UpdateDraftAsync(ContractId, request, CurrentResponsibleEmployeeId);
        var edited = await db.TblContractPaymentMilestones.OrderBy(x => x.DisplayOrder).ToListAsync();
        Assert.Equal(ids.AsEnumerable().Reverse(), edited.Select(x => x.PaymentMilestoneId));
        Assert.Equal(new decimal[] { 30, 70 }, edited.Select(x => x.Amount));
        Assert.Equal(new DateTime(2026, 10, 18), edited[1].AnchorDate);
        request = await PaymentUpdateRequest(db);
        request.Terms.RemoveAll(x => x.TermKind == ContractTermKind.Payment);
        await CreateService(db).UpdateDraftAsync(ContractId, request, CurrentResponsibleEmployeeId);
        Assert.Empty(db.TblContractPaymentMilestones);
        Assert.DoesNotContain(db.TblContractTerms, x => x.TermKind == (byte)ContractTermKind.Payment);
    }

    [Theory]
    [InlineData("duplicate-code")]
    [InlineData("over-100")]
    [InlineData("first-previous")]
    [InlineData("two-payment-terms")]
    [InlineData("general-with-milestones")]
    [InlineData("invalid-anchor")]
    [InlineData("foreign-id")]
    [InlineData("foreign-source")]
    [InlineData("invalid-percent-precision")]
    public async Task PaymentPlan_RejectsInvalidDraft(string scenario)
    {
        await using var db = CreateContext(); await SeedContractAsync(db);
        var request = CreateUpdateRequest();
        var term = request.Terms[0];
        term.TermKind = ContractTermKind.Payment;
        term.PaymentMilestones = [DraftMilestone("P1", 100)];
        switch (scenario)
        {
            case "duplicate-code": term.PaymentMilestones = [DraftMilestone("P1", 50), DraftMilestone("p1", 50)]; break;
            case "over-100": term.PaymentMilestones.Add(DraftMilestone("P2", 1)); break;
            case "first-previous": term.PaymentMilestones[0].DueAnchor = PaymentDueAnchor.PreviousMilestonePaid; break;
            case "two-payment-terms": request.Terms.Add(new() { TermCode = "OTHER", TermTitle = "Khác", TermKind = ContractTermKind.Payment }); break;
            case "general-with-milestones": term.TermKind = ContractTermKind.General; break;
            case "invalid-anchor": term.PaymentMilestones[0].DueAnchor = (PaymentDueAnchor)99; break;
            case "foreign-id": term.PaymentMilestones[0].PaymentMilestoneId = 999; break;
            case "foreign-source": term.PaymentMilestones[0].SourceTemplatePaymentMilestoneId = 999; break;
            case "invalid-percent-precision": term.PaymentMilestones[0].PaymentPercent = 99.12345m; break;
        }
        await Assert.ThrowsAsync<ArgumentException>(() => CreateService(db).UpdateDraftAsync(ContractId, request, CurrentResponsibleEmployeeId));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task PaymentPlan_RejectsChangesToPaidOrAcceptedMilestones(bool accepted, bool remove)
    {
        await using var db = CreateContext(); await SeedContractAsync(db);
        var request = CreateUpdateRequest();
        request.Terms[0].TermKind = ContractTermKind.Payment;
        request.Terms[0].PaymentMilestones = [DraftMilestone("P1", 100)];
        await CreateService(db).UpdateDraftAsync(ContractId, request, CurrentResponsibleEmployeeId);
        await StampPaymentRowVersions(db);
        var row = await db.TblContractPaymentMilestones.SingleAsync();
        if (accepted) db.TblContractAcceptanceMilestones.Add(new() { AcceptanceRecordId = 123, PaymentMilestoneId = row.PaymentMilestoneId });
        else { row.PaymentStatus = (byte)ContractPaymentMilestoneStatus.Paid; row.PaidAt = DateTime.UtcNow; row.PaidByEmployeeId = CurrentResponsibleEmployeeId; }
        await db.SaveChangesAsync();
        request = await PaymentUpdateRequest(db);
        if (remove) { request.Terms[0].PaymentMilestones.Clear(); request.Terms[0].TermKind = ContractTermKind.General; }
        else request.Terms[0].PaymentMilestones[0].TitleVi = "Đã sửa";
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService(db).UpdateDraftAsync(ContractId, request, CurrentResponsibleEmployeeId));
    }

    [Fact]
    public async Task PaymentPlan_RejectsStaleMilestoneRowVersion()
    {
        await using var db = CreateContext(); await SeedContractAsync(db);
        var request = CreateUpdateRequest();
        request.Terms[0].TermKind = ContractTermKind.Payment;
        request.Terms[0].PaymentMilestones = [DraftMilestone("P1", 100)];
        await CreateService(db).UpdateDraftAsync(ContractId, request, CurrentResponsibleEmployeeId);
        await StampPaymentRowVersions(db);
        request = await PaymentUpdateRequest(db);
        request.Terms[0].PaymentMilestones[0].RowVersion = Convert.ToBase64String(NewerRowVersion());
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => CreateService(db).UpdateDraftAsync(ContractId, request, CurrentResponsibleEmployeeId));
    }

    private static ContractPaymentMilestoneDraftRequest DraftMilestone(string code, decimal percent) => new() {
        MilestoneCode = code, TitleVi = code, PaymentPercent = percent, DueAnchor = PaymentDueAnchor.ContractSigned,
        DayCountMode = PaymentDayCountMode.CalendarDays };

    private static async Task StampPaymentRowVersions(DbDtctechContext db)
    {
        foreach (var term in await db.TblContractTerms.ToListAsync()) term.RowVersion = InitialRowVersion();
        foreach (var row in await db.TblContractPaymentMilestones.ToListAsync()) row.RowVersion = InitialRowVersion();
        await db.SaveChangesAsync();
    }

    private static async Task<UpdateContractDraftRequest> PaymentUpdateRequest(DbDtctechContext db)
    {
        var request = CreateUpdateRequest();
        var rows = await db.TblContractPaymentMilestones.OrderBy(x => x.DisplayOrder).ToListAsync();
        request.Terms = (await db.TblContractTerms.OrderBy(x => x.DisplayOrder).ToListAsync()).Select(term => new UpdateContractTermRequest {
            TermId = term.TermId, RowVersion = Convert.ToBase64String(term.RowVersion), TermCode = term.TermCode,
            TermTitle = term.TermTitle, TermKind = (ContractTermKind)term.TermKind, DisplayOrder = term.DisplayOrder,
            IsNegotiable = term.IsNegotiable, PaymentMilestones = rows.Where(x => x.TermId == term.TermId).Select(row => new ContractPaymentMilestoneDraftRequest {
                PaymentMilestoneId = row.PaymentMilestoneId, RowVersion = Convert.ToBase64String(row.RowVersion),
                MilestoneCode = row.MilestoneCode, TitleVi = row.TitleVi, PaymentPercent = row.PaymentPercent,
                DueAnchor = (PaymentDueAnchor)row.DueAnchor, DayCountMode = (PaymentDayCountMode)row.DayCountMode,
                DueOffsetDays = row.DueOffsetDays, AnchorDate = row.AnchorDate }).ToList()
        }).ToList();
        return request;
    }
}
