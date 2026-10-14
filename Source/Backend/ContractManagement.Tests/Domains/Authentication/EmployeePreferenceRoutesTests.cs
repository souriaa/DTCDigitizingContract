using ContractManagement.API.Common.Security;

namespace ContractManagement.Tests.Domains.Authentication;

public sealed class EmployeePreferenceRoutesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/missing")]
    [InlineData("/admin/employees")]
    public void UnsetOrUnavailablePage_DefaultsToContractList(string? storedPage)
    {
        Assert.Equal("/contracts", EmployeePreferenceRoutes.ResolveDefault(storedPage,
            [RbacPermissions.ContractReadOwn]));
    }

    [Theory]
    [InlineData("/dashboard")]
    [InlineData("/contracts")]
    [InlineData("/customers")]
    public void SavedAuthorizedSelection_IsPreserved(string storedPage)
    {
        Assert.Equal(storedPage, EmployeePreferenceRoutes.ResolveDefault(storedPage,
            [RbacPermissions.ContractReadOwn, RbacPermissions.CustomerManage]));
    }

    [Fact]
    public void NoContractPermission_DoesNotRedirectToUnauthorizedContractPage()
    {
        Assert.Equal("/dashboard", EmployeePreferenceRoutes.ResolveDefault(null, []));
    }

    [Fact]
    public void ContractDefault_HasClearLabelAndIsFirstAvailableOption()
    {
        var option = EmployeePreferenceRoutes.GetAvailable([RbacPermissions.ContractReadOwn])[0];
        Assert.Equal("/contracts", option.Path);
        Assert.Equal("Danh sách hợp đồng", option.Label);
    }
}
