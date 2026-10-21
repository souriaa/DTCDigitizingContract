using ContractManagement.Infrastructure.Persistence.Application.Models;

namespace ContractManagement.Domains.Policies.ContractTemplate;

/// <summary>Adapts the signed relational snapshot to the same source registry; never queries live party profiles.</summary>
public static class FrozenContractPartySources
{
    public static TblCustomer Counterparty(TblContractVersionPartySnapshot x) => new()
    {
        CustomerId = x.SourceCustomerId ?? throw new InvalidOperationException("Snapshot đối tác thiếu định danh nguồn."),
        CustomerCode = x.SubjectCode, CustomerFullName = x.FullName, CustomerCompany = x.LegalName,
        CustomerCompanyEn = x.LegalNameEn, CustomerAddress = x.Address, CustomerAddressEn = x.AddressEn,
        CustomerRepresentativeName = x.RepresentativeName, CustomerRepresentativeTitle = x.RepresentativeTitle,
        CustomerRepresentativeTitleEn = x.RepresentativeTitleEn,
        CustomerContactPersonName = x.ContactName, CustomerContactPersonTitle = x.ContactTitle,
        CustomerContactPersonTitleEn = x.ContactTitleEn, CustomerContactPersonPhone = x.ContactPhone,
        CustomerPhone = x.PhoneNumber, CustomerMobile = x.Mobile, CustomerFaxNumber = x.FaxNumber,
        CustomerTaxCode = x.TaxCode, CustomerBankAccountNumber = x.BankAccountNumber,
        CustomerBankName = x.BankName, CustomerBankNameEn = x.BankNameEn,
        CustomerEmail = x.Email, CustomerWebsite = x.Website, CustomerCity = x.City,
        CustomerCountry = x.Country, CustomerCityEn = x.CityEn, CustomerCountryEn = x.CountryEn, CustomerZipCode = x.ZipCode
    };

    public static TblTenantLegalProfile Tenant(TblContractVersionPartySnapshot x) => new()
    {
        LegalEntityName = x.LegalName, LegalEntityNameEn = x.LegalNameEn!,
        Address = x.Address, AddressEn = x.AddressEn!, TaxCode = x.TaxCode!,
        RepresentativeName = x.RepresentativeName, RepresentativeTitle = x.RepresentativeTitle,
        RepresentativeTitleEn = x.RepresentativeTitleEn!, PhoneNumber = x.PhoneNumber, FaxNumber = x.FaxNumber,
        BankAccountNumber = x.BankAccountNumber, BankName = x.BankName, BankNameEn = x.BankNameEn!
    };
}
