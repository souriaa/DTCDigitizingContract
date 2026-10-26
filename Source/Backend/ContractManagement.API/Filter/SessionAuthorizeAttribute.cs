using ContractManagement.API.Common.Security;
using ContractManagement.Infrastructure.MultiTenancy.Interfaces;
using ContractManagement.Infrastructure.Persistence.Application;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ContractManagement.Filter;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class SessionAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string[] _requiredPermissions;
    public SessionAuthorizeAttribute(params string[] requiredPermissions) => _requiredPermissions = requiredPermissions ?? [];
    public bool RequireAnyPermission { get; set; }
    public bool AllowWhenPasswordChangeRequired { get; set; }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var services = context.HttpContext.RequestServices;
        try
        {
            var employee = await EmployeeSessionValidator.ValidateAsync(context.HttpContext,
                services.GetRequiredService<ICurrentTenant>(), services.GetRequiredService<DbDtctechContext>(),
                RequireAnyPermission ? [] : _requiredPermissions, AllowWhenPasswordChangeRequired);
            if (RequireAnyPermission && !_requiredPermissions.Any(employee.Permissions.Contains))
                throw new RbacOperationException(403, AuthorizationErrorCodes.PermissionDenied,
                    "Employee does not have the required permission.");
        }
        catch (RbacOperationException exception)
        {
            context.Result = new ObjectResult(new AuthorizationErrorResponse(exception.Code, exception.Message))
            {
                StatusCode = exception.StatusCode
            };
        }
    }
}
