using System.ComponentModel.DataAnnotations;
using ContractManagement.API.Common.Enums;
using ContractManagement.API.Common.Exceptions;
using ContractManagement.API.Domains.DTOs.Requests.Contract;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Tests.Domains.Services.Contract;

public sealed partial class ContractAcceptanceServicePhase7Tests
{
    [Theory]
    [InlineData("missing")]
    [InlineData("default")]
    [InlineData("before")]
    [InlineData("future")]
    public async Task Signing_InvalidBusinessDateDoesNotSignOrAnchorAndCleansUploadedFile(string scenario)
    {
        await using var db = CreateContext();
        var storage = new TrackingStorage();
        await SeedAsync(db, storage);
        var service = CreateService(db, storage);
        var draft = await service.CreateAsync(ContractId, CreateRequest(), EmployeeId);
        draft = await SelectFixtureHandoverScopeAsync(db, storage, service, draft);
        var finalized = await service.FinalizeAsync(ContractId, draft.AcceptanceRecordId,
            new() { RowVersion = draft.RowVersion }, EmployeeId);
        DateTime? signDate = scenario switch
        {
            "missing" => null,
            "default" => default(DateTime),
            "before" => finalized.AcceptanceDate.AddDays(-1),
            _ => DateTime.UtcNow.AddHours(7).Date.AddDays(1)
        };
        var fileCount = await db.TblFileStorages.CountAsync();
        var deleted = storage.DeletedKeys.Count;
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => service.UploadSignedEvidenceAsync(
            ContractId, finalized.AcceptanceRecordId, new() { RowVersion = finalized.RowVersion,
                SignDate = signDate, File = FormFile("signed.pdf", "application/pdf", "%PDF-sign") }, EmployeeId));
        Assert.Equal("AcceptanceSignDateInvalid", error.Code);
        db.ChangeTracker.Clear();
        var row = await db.TblContractAcceptanceRecords.SingleAsync();
        Assert.Equal((byte)AcceptanceRecordStatus.Finalized, row.Status);
        Assert.Null(row.SignedAt);
        Assert.Empty(db.TblContractAcceptanceEvidences);
        Assert.All(await db.TblContractPaymentMilestones.ToListAsync(), x => Assert.Null(x.AnchorDate));
        Assert.Equal(fileCount, await db.TblFileStorages.CountAsync());
        Assert.Equal(deleted + 1, storage.DeletedKeys.Count);
    }

    [Fact]
    public void UploadSignedEvidenceRequest_RequiresExplicitBusinessDate()
    {
        var request = new UploadContractAcceptanceSignedEvidenceRequest { RowVersion = "version" };
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), errors, true));
        Assert.Contains(errors, x => x.MemberNames.Contains(nameof(request.SignDate)));
    }
}
