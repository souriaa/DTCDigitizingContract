using System.ComponentModel.DataAnnotations;
using ContractManagement.Infrastructure.Security;

namespace ContractManagement.API.Common.Security;

// Composition belongs to new passwords only. Existing/login passwords are not revalidated.
// Reuse checks remain in services, against the stored hash (never expose it to the client).
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class NewAccountPasswordAttribute : ValidationAttribute
{
    public NewAccountPasswordAttribute() => ErrorMessage = PasswordRules.ValidationMessage;
    public override bool IsValid(object? value) => value is null || value is string password && PasswordRules.IsValid(password);
}
