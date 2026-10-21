namespace ContractManagement.API.Common.Enums;

/// <summary>Direction of the transaction from the owning tenant's perspective.</summary>
public enum ContractDirection : byte
{
    Sale = 1,
    Purchase = 2
}
