using System.ComponentModel.DataAnnotations;
using ContractManagement.API.Common.Security;
using ContractManagement.API.Domains.DTOs.Requests.Authentication;
using ContractManagement.API.Domains.DTOs.Requests.Employee;
using ContractManagement.API.Domains.DTOs.Requests.SystemAuthentication;
using ContractManagement.API.Domains.Recovery;
using ContractManagement.Contracts.Tenants;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using ContractManagement.Infrastructure.Persistence.Central.Entities;
using ContractManagement.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;

namespace ContractManagement.Tests.Domains.Security;

public sealed class AccountPasswordPolicyTests
{
    [Theory]
    [InlineData("Abcdefghij12", true)]
    [InlineData("Abcdefghij1", false)]
    [InlineData("abcdefghij12", false)]
    [InlineData("ABCDEFGHIJ12", false)]
    [InlineData("Abcdefghijkl", false)]
    [InlineData("123456789012", false)]
    [InlineData("Abcdefghij١٢", false)]
    [InlineData("Abcdefghij１２", false)]
    [InlineData(" Abcdefghij1 ", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void SharedRule_CompositionAndLengthAreMandatory(string? password, bool valid)
    {
        Assert.Equal(valid, PasswordRules.IsValid(password));
        if (valid) AccountPasswordPolicy.EnsureValid(password);
        else Assert.Equal(AuthorizationErrorCodes.PasswordPolicyViolation,
            Assert.Throws<RbacOperationException>(() => AccountPasswordPolicy.EnsureValid(password)).Code);
    }

    [Fact]
    public void Exactly100Characters_IsAccepted_101IsRejected()
    {
        var password = "Aa" + new string('1', 98);
        Assert.Equal(100, password.Length);
        AccountPasswordPolicy.EnsureValid(password);
        Assert.Throws<RbacOperationException>(() => AccountPasswordPolicy.EnsureValid(password + "1"));
    }

    [Fact]
    public void SamePassword_IsRejectedAgainstBothAccountHashes()
    {
        const string current = "CurrentPassword123!";
        var employee = new TblEmployee();
        var employeeHasher = new PasswordHasher<TblEmployee>();
        var employeeHash = employeeHasher.HashPassword(employee, current);
        Assert.Equal(AuthorizationErrorCodes.PasswordReuseNotAllowed, Assert.Throws<RbacOperationException>(() =>
            AccountPasswordPolicy.EnsureNotReused(employeeHasher, employee, employeeHash, current)).Code);
        AccountPasswordPolicy.EnsureNotReused(employeeHasher, employee, employeeHash, "DifferentPassword123!");
        var admin = new SystemAdmin();
        var adminHasher = new PasswordHasher<SystemAdmin>();
        var adminHash = adminHasher.HashPassword(admin, current);
        Assert.Equal(AuthorizationErrorCodes.PasswordReuseNotAllowed, Assert.Throws<RbacOperationException>(() =>
            AccountPasswordPolicy.EnsureNotReused(adminHasher, admin, adminHash, current)).Code);
        AccountPasswordPolicy.EnsureNotReused(adminHasher, admin, adminHash, "DifferentPassword123!");
    }

    [Fact]
    public void EveryNewPasswordDto_ValidatesComposition_NotCurrentPasswords()
    {
        (object Model, string Member)[] targets = [
            (new ChangeOwnPasswordRequest(), nameof(ChangeOwnPasswordRequest.NewPassword)),
            (new ChangeSystemAdminPasswordRequest(), nameof(ChangeSystemAdminPasswordRequest.NewPassword)),
            (new ChangePasswordRequest(), nameof(ChangePasswordRequest.NewPassword)),
            (new CreateEmployeeRequest(), nameof(CreateEmployeeRequest.EmployeePassword)),
            (new InitialManagerRequest(), nameof(InitialManagerRequest.EmployeePassword)),
            (new PasswordRecoveryReset(), nameof(PasswordRecoveryReset.NewPassword)),
            (new TenantPasswordRecoveryReset(), nameof(TenantPasswordRecoveryReset.NewPassword)),
        ];
        foreach (var (model, member) in targets)
        {
            var context = new ValidationContext(model) { MemberName = member };
            foreach (var invalid in new[] { "lowercase12345", "UPPERCASE12345", "MissingDigitsHere", "123456789012" })
                Assert.False(Validator.TryValidateProperty(invalid, context, new List<ValidationResult>()));
            Assert.True(Validator.TryValidateProperty("ValidPassword123", context, new List<ValidationResult>()));
            Assert.True(Validator.TryValidateProperty("Aa" + new string('1', 98), context, new List<ValidationResult>()));
        }
        Assert.True(Validator.TryValidateProperty("old-password", new(new ChangeOwnPasswordRequest()) { MemberName = "CurrentPassword" }, new List<ValidationResult>()));
        Assert.True(Validator.TryValidateProperty("old-password", new(new ChangeSystemAdminPasswordRequest()) { MemberName = "CurrentPassword" }, new List<ValidationResult>()));
    }
}
