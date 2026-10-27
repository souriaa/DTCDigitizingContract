using ContractManagement.API.Common.Enums;
using ContractManagement.API.Common.Security;
using ContractManagement.Domains.Services.Contract;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Tests.Domains.Services.Contract;

public sealed partial class ContractAcceptanceServicePhase7Tests
{
    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("references")]
    [InlineData("sections")]
    [InlineData("milestones")]
    [InlineData("finalize")]
    [InlineData("signed-evidence")]
    [InlineData("cancel")]
    public async Task Technical_CannotMutateAnotherOwnersAcceptanceThroughAnyAction(string action)
    {
        await using var db = CreateContext();
        var storage = new TrackingStorage();
        await SeedAsync(db, storage);
        (await db.TblEmployees.SingleAsync()).EmployeeType = (byte)EmployeeType.Technical;
        var actorId = EmployeeId + 10000;
        db.TblEmployees.Add(new TblEmployee {
            EmployeeId = actorId, EmployeeFullName = "Other technical", Status = 1,
            EmployeeType = (byte)EmployeeType.Technical });
        await db.SaveChangesAsync();
        var service = CreateService(db, storage,
            authorization: new ContractResourceAuthorizationService(db));
        var draft = await service.CreateAsync(ContractId, CreateRequest(), EmployeeId);
        Func<Task> operation = action switch {
            "create" => () => service.CreateAsync(ContractId, CreateRequest(), actorId),
            "update" => () => service.UpdateAsync(ContractId, draft.AcceptanceRecordId,
                new() { RowVersion = draft.RowVersion }, actorId),
            "references" => () => service.ReplaceReferencesAsync(ContractId, draft.AcceptanceRecordId,
                new() { RowVersion = draft.RowVersion }, actorId),
            "sections" => () => service.ReplaceSectionsAsync(ContractId, draft.AcceptanceRecordId,
                new() { RowVersion = draft.RowVersion }, actorId),
            "milestones" => () => service.ReplaceMilestonesAsync(ContractId, draft.AcceptanceRecordId,
                new() { RowVersion = draft.RowVersion }, actorId),
            "finalize" => () => service.FinalizeAsync(ContractId, draft.AcceptanceRecordId,
                new() { RowVersion = draft.RowVersion }, actorId),
            "signed-evidence" => () => service.UploadSignedEvidenceAsync(ContractId, draft.AcceptanceRecordId,
                new() { RowVersion = draft.RowVersion, SignDate = new DateTime(2026, 9, 20),
                    File = FormFile("signed.pdf", "application/pdf", "%PDF-signed") }, actorId),
            "cancel" => () => service.CancelAsync(ContractId, draft.AcceptanceRecordId,
                new() { RowVersion = draft.RowVersion, Reason = "Hủy biên bản." }, actorId),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };
        var error = await Assert.ThrowsAsync<RbacOperationException>(operation);
        Assert.Equal(403, error.StatusCode);
        Assert.Single(db.TblContractAcceptanceRecords);
        Assert.Equal((byte)AcceptanceRecordStatus.Draft,
            (await db.TblContractAcceptanceRecords.SingleAsync()).Status);
        Assert.Empty(storage.SavedKeys);
        Assert.Empty(storage.DeletedKeys);
        Assert.Empty(db.TblContractAcceptanceEvidences);
    }

    [Fact]
    public async Task TechnicalOwner_CanCreateFinalizeAndUploadOwnAcceptanceSignedEvidence()
    {
        await using var db = CreateContext();
        var storage = new TrackingStorage();
        await SeedAsync(db, storage);
        (await db.TblEmployees.SingleAsync()).EmployeeType = (byte)EmployeeType.Technical;
        await db.SaveChangesAsync();
        var service = CreateService(db, storage,
            authorization: new ContractResourceAuthorizationService(db));
        var draft = await service.CreateAsync(ContractId, CreateRequest(), EmployeeId);
        var finalized = await service.FinalizeAsync(ContractId, draft.AcceptanceRecordId,
            new() { RowVersion = draft.RowVersion }, EmployeeId);
        await service.UploadSignedEvidenceAsync(ContractId, draft.AcceptanceRecordId,
            new() { RowVersion = finalized.RowVersion, SignDate = new DateTime(2026, 9, 20),
                File = FormFile("signed.pdf", "application/pdf", "%PDF-signed") }, EmployeeId);
        Assert.Equal(AcceptanceRecordStatus.Signed,
            (await service.GetAsync(ContractId, draft.AcceptanceRecordId, EmployeeId)).Status);
    }

    [Fact]
    public async Task TechnicalOwner_LosesAcceptanceWriteAccessImmediatelyAfterResponsibilityTransfer()
    {
        await using var db = CreateContext();
        var storage = new TrackingStorage();
        await SeedAsync(db, storage);
        (await db.TblEmployees.SingleAsync()).EmployeeType = (byte)EmployeeType.Technical;
        await db.SaveChangesAsync();
        var service = CreateService(db, storage,
            authorization: new ContractResourceAuthorizationService(db));
        var draft = await service.CreateAsync(ContractId, CreateRequest(), EmployeeId);
        (await db.TblContracts.SingleAsync()).EmployeeId = EmployeeId + 10000;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<RbacOperationException>(() => service.FinalizeAsync(
            ContractId, draft.AcceptanceRecordId, new() { RowVersion = draft.RowVersion }, EmployeeId));
        Assert.Empty(storage.SavedKeys);
        Assert.Equal((byte)AcceptanceRecordStatus.Draft,
            (await db.TblContractAcceptanceRecords.SingleAsync()).Status);
    }
}
