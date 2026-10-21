using ContractManagement.API.Common.Enums;

namespace ContractManagement.Domains.Policies.Contract;

public enum ContractPartySubject : byte { Tenant = 1, Counterparty = 2 }
public sealed record ResolvedContractParty<T>(ContractPartySubject Subject, T Value);
public sealed record ResolvedContractParties<T>(ResolvedContractParty<T> Provider, ResolvedContractParty<T> Customer,
    bool TenantReceivesPayment);

/// <summary>Pure role assignment. Never changes subject identity, authorization or storage ownership.</summary>
public static class ContractPartyRoleResolver
{
    public static void Validate(ContractDirection direction)
    {
        if (!Enum.IsDefined(direction))
            throw new ArgumentException("Chiều giao dịch phải là Bán hoặc Mua.", nameof(direction));
    }

    public static ResolvedContractParties<T> Resolve<T>(ContractDirection direction, T tenant, T counterparty)
    {
        Validate(direction);
        var internalParty = new ResolvedContractParty<T>(ContractPartySubject.Tenant, tenant);
        var externalParty = new ResolvedContractParty<T>(ContractPartySubject.Counterparty, counterparty);
        return direction == ContractDirection.Sale
            ? new(internalParty, externalParty, true)
            : new(externalParty, internalParty, false);
    }
}
