namespace ContractManagement.API.Common.Enums;

/// <summary>Deleted catalog rows are retained for historical document references.</summary>
public static class CatalogItemStatuses
{
    public const byte Inactive = 0;
    public const byte Active = 1;
    public const byte Deleted = 2;
}
