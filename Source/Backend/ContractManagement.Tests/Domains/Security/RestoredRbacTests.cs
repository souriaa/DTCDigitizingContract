using ContractManagement.API.Common.Enums;
using ContractManagement.API.Common.Security;
using ContractManagement.Domains.Services.Contract;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Tests.Domains.Security;

public sealed class RestoredRbacTests
{
    [Theory]
    [InlineData(EmployeeType.Sale)]
    [InlineData(EmployeeType.Marketing)]
    [InlineData(EmployeeType.Technical)]
    [InlineData(EmployeeType.Manager)]
    [InlineData(EmployeeType.AdminOfficer)]
    [InlineData(EmployeeType.Accountant)]
    public async Task RoleAndOwnerMatrix_IsEnforcedInTenantDatabase(EmployeeType type)
    {
        await using var db = new DbDtctechContext(new DbContextOptionsBuilder<DbDtctechContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString(), x => x.EnableNullChecks(false)).Options);
        db.TblEmployees.Add(new() { EmployeeId = 1, EmployeeType = (byte)type, Status = 1 });
        db.TblContracts.AddRange(new TblContract { ContractId = 10, EmployeeId = 1, Status = (byte)ContractStatus.Signed },
            new TblContract { ContractId = 20, EmployeeId = 2, Status = (byte)ContractStatus.Signed });
        await db.SaveChangesAsync();
        var service = new ContractResourceAuthorizationService(db);
        var global = type is EmployeeType.Manager or EmployeeType.AdminOfficer or EmployeeType.Accountant;
        var canSign = type is EmployeeType.Manager or EmployeeType.AdminOfficer;
        foreach (var id in new[] { 10, 20 })
        {
            if (canSign) await service.EnsureCanManageSigningAsync(id, 1);
            else await Assert.ThrowsAsync<RbacOperationException>(() => service.EnsureCanManageSigningAsync(id, 1));
            if (global) await service.EnsureCanCompleteAsync(id, 1);
            else await Assert.ThrowsAsync<RbacOperationException>(() => service.EnsureCanCompleteAsync(id, 1));
            if (global || id == 10)
            {
                await service.EnsureCanManageAcceptanceAsync(id, 1);
                await service.EnsureCanManagePaymentAsync(id, 1);
            }
            else
            {
                await Assert.ThrowsAsync<RbacOperationException>(() => service.EnsureCanManageAcceptanceAsync(id, 1));
                await Assert.ThrowsAsync<RbacOperationException>(() => service.EnsureCanManagePaymentAsync(id, 1));
            }
        }
        Assert.Equal(type == EmployeeType.AdminOfficer, EmployeePermissionCatalog.HasPermission((byte)type, RbacPermissions.ContractApprovalReview));
        Assert.Equal(type == EmployeeType.Manager, EmployeePermissionCatalog.HasPermission((byte)type, RbacPermissions.ContractApprovalDecide));
        (await db.TblContracts.SingleAsync(x => x.ContractId == 10)).EmployeeId = 2;
        await db.SaveChangesAsync();
        if (!global) await Assert.ThrowsAsync<RbacOperationException>(() => service.EnsureCanManageAcceptanceAsync(10, 1));
    }
}
