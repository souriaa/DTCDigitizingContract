using System.Security.Cryptography;
using System.Text;
using ContractManagement.Infrastructure.Persistence.Application.Models;

namespace ContractManagement.API.Domains.Models.Contract;

/// <summary>
/// Schema pháp lý bất biến dùng chung cho renderer và artifact của SoftwareSupply.
/// Không chứa RowVersion hoặc dữ liệu master có thể thay đổi sau submit.
/// </summary>
public sealed record SoftwareSupplyContractSnapshot(
    TenantLegalSnapshot Tenant,
    CustomerLegalSnapshot Customer,
    ContractLegalSnapshot Contract,
    ContractVersionLegalSnapshot Version,
    IReadOnlyList<ContractItemLegalSnapshot> Items,
    IReadOnlyList<ContractTermLegalSnapshot> Terms)
{
    public IReadOnlyDictionary<string, string>? PlaceholderValues { get; init; }

    public IReadOnlyList<ContractLegalBasisSnapshot>? LegalBases { get; init; }

    public IReadOnlyList<ContractPaymentMilestoneSnapshot>? PaymentMilestones { get; init; }

    public IReadOnlyList<ContractAppendixLegalSnapshot>? Appendices { get; init; }
}

public sealed record ContractAppendixLegalSnapshot(
    int AppendixId,
    int? SourceTemplateAppendixId,
    string AppendixCode,
    string AppendixName,
    string? AppendixNameEn,
    string? AppendixDescription,
    bool IsRequired,
    int DisplayOrder,
    IReadOnlyList<ContractAppendixTermLegalSnapshot> Terms);

public sealed record ContractAppendixTermLegalSnapshot(
    int AppendixTermId,
    int? SourceTemplateAppendixTermId,
    string TermCode,
    string TermTitle,
    string? TermTitleEn,
    string? TermContent,
    string? TermContentEn,
    int DisplayOrder)
{
    public byte BilingualContentMode { get; init; } = 1;
}

public sealed record TenantLegalSnapshot(
    string LegalEntityName,
    string TaxCode,
    string Address,
    string RepresentativeName,
    string RepresentativeTitle,
    string? PhoneNumber,
    string? FaxNumber,
    string? BankAccountNumber,
    string? BankName)
{
    public string? LegalNameEn { get; init; }
    public string? AddressEn { get; init; }
    public string? RepresentativeTitleEn { get; init; }
    public string? ContactTitleEn { get; init; }
    public string? BankNameEn { get; init; }
    public string? CityEn { get; init; }
    public string? CountryEn { get; init; }
}

public sealed record CustomerLegalSnapshot(
    int CustomerId,
    string LegalName,
    string? TaxCode,
    string Address,
    string RepresentativeName,
    string RepresentativeTitle,
    string? PhoneNumber,
    string? FaxNumber,
    string? BankAccountNumber,
    string? BankName)
{
    public string? SubjectCode { get; init; }
    public string? ContactName { get; init; }
    public string? ContactTitle { get; init; }
    public string? ContactPhone { get; init; }
    public string? FullName { get; init; }
    public string? Email { get; init; }
    public string? Website { get; init; }
    public string? City { get; init; }
    public string? Country { get; init; }
    public string? Mobile { get; init; }
    public string? ZipCode { get; init; }
    public string? LegalNameEn { get; init; }
    public string? AddressEn { get; init; }
    public string? RepresentativeTitleEn { get; init; }
    public string? ContactTitleEn { get; init; }
    public string? BankNameEn { get; init; }
    public string? CityEn { get; init; }
    public string? CountryEn { get; init; }
}

public sealed record ContractLegalSnapshot(
    int ContractId,
    string ContractCode,
    string ContractName,
    string? ContractNameEn,
    byte ContractType,
    int TemplateVersionId,
    DateTime CreatedDate,
    DateTime? SignDate,
    DateTime? EffectiveDate,
    DateTime? ExpireDate,
    string CurrencyCode,
    byte LanguageMode,
    decimal Subtotal,
    decimal TotalDiscount,
    decimal TotalVat,
    decimal TotalAmount)
{
    public byte ContractDirection { get; init; }
}

public sealed record ContractVersionLegalSnapshot(
    int VersionId,
    int VersionNo,
    int? SourceVersionId,
    int TemplateVersionId,
    string CurrencyCode,
    decimal Subtotal,
    decimal TotalDiscount,
    decimal TotalVat,
    decimal TotalAmount);

public sealed record ContractItemLegalSnapshot(
    int ContractItemId,
    byte ItemType,
    string? ItemCode,
    string ItemName,
    string? ItemNameEn,
    string? ItemDescription,
    string? ItemDescriptionEn,
    string? UnitName,
    string? UnitNameEn,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineSubtotal,
    byte DiscountMode,
    decimal DiscountPercent,
    decimal FixedDiscountAmount,
    decimal DiscountAmount,
    bool IsTaxable,
    decimal VatPercent,
    decimal VatAmount,
    decimal LineTotal,
    int DisplayOrder);

public sealed record ContractTermLegalSnapshot(
    int TermId,
    string TermCode,
    string TermTitle,
    string? TermTitleEn,
    string? TermContent,
    string? TermContentEn,
    bool IsNegotiable,
    int DisplayOrder)
{
    public byte TermKind { get; init; }
    public byte BilingualContentMode { get; init; } = 1;
}

public sealed record ContractPaymentMilestoneSnapshot(
    int PaymentMilestoneId,
    int TermId,
    int? SourceTemplatePaymentMilestoneId,
    string MilestoneCode,
    string TitleVi,
    string? TitleEn,
    decimal PaymentPercent,
    byte DueAnchor,
    int DueOffsetDays,
    byte DayCountMode,
    string? ConditionVi,
    string? ConditionEn,
    int DisplayOrder,
    decimal Amount,
    DateTime? AnchorDate,
    DateTime? DueDate,
    byte PaymentStatus,
    DateTime? PaidAt,
    int? PaidByEmployeeId);

public sealed record ContractLegalBasisSnapshot(
    int LegalBasisId,
    string BasisCode,
    string ContentVi,
    string? ContentEn,
    int DisplayOrder);

public static class SoftwareSupplyContractSnapshotFactory
{
    public static SoftwareSupplyContractSnapshot Create(
        TblTenantLegalProfile tenant,
        TblCustomer customer,
        TblContract contract,
        TblContractVersion version,
        IEnumerable<TblContractItem> items,
        IEnumerable<TblContractTerm> terms,
        IEnumerable<TblContractPaymentMilestone>? paymentMilestones = null,
        IEnumerable<TblContractAppendix>? appendices = null,
        IEnumerable<TblContractAppendixTerm>? appendixTerms = null)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(terms);
        ContractManagement.Domains.Policies.Contract.ContractPartyRoleResolver.Validate(
            (ContractManagement.API.Common.Enums.ContractDirection)contract.ContractDirection);

        var customerLegalName = FirstRequired(
            customer.CustomerCompany,
            customer.CustomerFullName,
            "Tên pháp lý khách hàng");
        var representativeName = Required(
            customer.CustomerRepresentativeName,
            "Người đại diện khách hàng");

        return new SoftwareSupplyContractSnapshot(
            new TenantLegalSnapshot(
                Required(tenant.LegalEntityName, "Tên pháp nhân tenant"),
                Required(tenant.TaxCode, "Mã số thuế tenant"),
                Required(tenant.Address, "Địa chỉ tenant"),
                Required(tenant.RepresentativeName, "Người đại diện tenant"),
                Required(tenant.RepresentativeTitle, "Chức danh đại diện tenant"),
                Optional(tenant.PhoneNumber),
                Optional(tenant.FaxNumber),
                Optional(tenant.BankAccountNumber),
                Optional(tenant.BankName))
            {
                LegalNameEn = tenant.LegalEntityNameEn, AddressEn = tenant.AddressEn,
                RepresentativeTitleEn = tenant.RepresentativeTitleEn, BankNameEn = tenant.BankNameEn
            },
            new CustomerLegalSnapshot(
                customer.CustomerId,
                customerLegalName,
                customer.CustomerTaxCode?.Trim(),
                Required(customer.CustomerAddress, "Địa chỉ khách hàng"),
                representativeName,
                Required(
                    customer.CustomerRepresentativeTitle,
                    "Chức danh đại diện khách hàng"),
                FirstOptional(customer.CustomerPhone, customer.CustomerMobile),
                Optional(customer.CustomerFaxNumber),
                Optional(customer.CustomerBankAccountNumber),
                Optional(customer.CustomerBankName))
            {
                SubjectCode = customer.CustomerCode, FullName = customer.CustomerFullName,
                ContactName = customer.CustomerContactPersonName, ContactTitle = customer.CustomerContactPersonTitle,
                ContactPhone = customer.CustomerContactPersonPhone, Mobile = customer.CustomerMobile,
                Email = customer.CustomerEmail, Website = customer.CustomerWebsite, City = customer.CustomerCity,
                Country = customer.CustomerCountry, ZipCode = customer.CustomerZipCode,
                LegalNameEn = customer.CustomerCompanyEn, AddressEn = customer.CustomerAddressEn,
                RepresentativeTitleEn = customer.CustomerRepresentativeTitleEn,
                ContactTitleEn = customer.CustomerContactPersonTitleEn, BankNameEn = customer.CustomerBankNameEn,
                CityEn = customer.CustomerCityEn, CountryEn = customer.CustomerCountryEn
            },
            new ContractLegalSnapshot(
                contract.ContractId,
                Required(contract.ContractCode, "Mã hợp đồng"),
                Required(contract.ContractName, "Tên hợp đồng"),
                contract.ContractNameEn?.Trim(),
                contract.ContractType,
                contract.TemplateVersionId,
                contract.CreatedDate,
                contract.SignDate,
                contract.EffectiveDate,
                contract.ExpireDate,
                Required(contract.CurrencyCode, "Tiền tệ"),
                contract.LanguageMode,
                contract.Subtotal,
                contract.TotalDiscount,
                contract.TotalVat,
                contract.TotalAmount) { ContractDirection = contract.ContractDirection },
            new ContractVersionLegalSnapshot(
                version.VersionId,
                version.VersionNo,
                version.SourceVersionId,
                version.TemplateVersionId,
                Required(version.CurrencyCode, "Tiền tệ version"),
                version.Subtotal,
                version.TotalDiscount,
                version.TotalVat,
                version.TotalAmount),
            items.OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.ContractItemId)
                .Select(x => new ContractItemLegalSnapshot(
                    x.ContractItemId,
                    x.ItemType,
                    x.ItemCode,
                    x.ItemName,
                    x.ItemNameEn,
                    x.ItemDescription,
                    x.ItemDescriptionEn,
                    x.UnitName,
                    x.UnitNameEn,
                    x.Quantity,
                    x.UnitPrice,
                    x.LineSubtotal,
                    x.DiscountMode,
                    x.DiscountPercent,
                    x.FixedDiscountAmount,
                    x.DiscountAmount,
                    x.IsTaxable,
                    x.VatPercent,
                    x.VatAmount,
                    x.LineTotal,
                    x.DisplayOrder))
                .ToArray(),
            terms.OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.TermId)
                .Select(x => new ContractTermLegalSnapshot(
                    x.TermId,
                    x.TermCode,
                    x.TermTitle,
                    x.TermTitleEn,
                    x.TermContent,
                    x.TermContentEn,
                    x.IsNegotiable,
                    x.DisplayOrder)
                {
                    TermKind = x.TermKind,
                    BilingualContentMode = x.BilingualContentMode
                })
                .ToArray())
        {
            PaymentMilestones = paymentMilestones?.OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.PaymentMilestoneId)
                .Select(x => new ContractPaymentMilestoneSnapshot(
                    x.PaymentMilestoneId, x.TermId,
                    x.SourceTemplatePaymentMilestoneId, x.MilestoneCode,
                    x.TitleVi, x.TitleEn, x.PaymentPercent, x.DueAnchor,
                    x.DueOffsetDays, x.DayCountMode, x.ConditionVi,
                    x.ConditionEn, x.DisplayOrder, x.Amount,
                    x.AnchorDate, x.DueDate, x.PaymentStatus,
                    x.PaidAt, x.PaidByEmployeeId)).ToArray(),
            Appendices = appendices?.OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.AppendixId)
                .Select(x => new ContractAppendixLegalSnapshot(
                    x.AppendixId, x.SourceTemplateAppendixId,
                    x.AppendixCode, x.AppendixName, x.AppendixNameEn,
                    x.AppendixDescription, x.IsRequired, x.DisplayOrder,
                    (appendixTerms ?? []).Where(term =>
                            term.AppendixId == x.AppendixId)
                        .OrderBy(term => term.DisplayOrder)
                        .ThenBy(term => term.AppendixTermId)
                        .Select(term => new ContractAppendixTermLegalSnapshot(
                            term.AppendixTermId,
                            term.SourceTemplateAppendixTermId,
                            term.TermCode, term.TermTitle, term.TermTitleEn,
                            term.TermContent, term.TermContentEn,
                            term.DisplayOrder) { BilingualContentMode = term.BilingualContentMode }).ToArray())).ToArray()
        };
    }

    public static TblContractVersionLegalSnapshot CreatePersistenceGraph(
        SoftwareSupplyContractSnapshot snapshot,
        int createdByEmployeeId,
        DateTime createdDate)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (createdByEmployeeId <= 0)
            throw new ArgumentOutOfRangeException(nameof(createdByEmployeeId));

        var legal = new TblContractVersionLegalSnapshot
        {
            VersionId = snapshot.Version.VersionId,
            ContractId = snapshot.Contract.ContractId,
            VersionNo = snapshot.Version.VersionNo,
            SourceVersionId = snapshot.Version.SourceVersionId,
            TemplateVersionId = snapshot.Version.TemplateVersionId,
            ContractCode = snapshot.Contract.ContractCode,
            ContractName = snapshot.Contract.ContractName,
            ContractNameEn = snapshot.Contract.ContractNameEn,
            ContractType = snapshot.Contract.ContractType,
            ContractDirection = snapshot.Contract.ContractDirection,
            ContractCreatedDate = snapshot.Contract.CreatedDate,
            SignDate = snapshot.Contract.SignDate,
            EffectiveDate = snapshot.Contract.EffectiveDate,
            ExpireDate = snapshot.Contract.ExpireDate,
            CurrencyCode = snapshot.Contract.CurrencyCode,
            LanguageMode = snapshot.Contract.LanguageMode,
            Subtotal = snapshot.Version.Subtotal,
            TotalDiscount = snapshot.Version.TotalDiscount,
            TotalVat = snapshot.Version.TotalVat,
            TotalAmount = snapshot.Version.TotalAmount,
            CreatedDate = createdDate,
            CreatedByEmployeeId = createdByEmployeeId
        };
        legal.Parties.Add(new TblContractVersionPartySnapshot
        {
            PartyRole = 1,
            LegalName = snapshot.Tenant.LegalEntityName,
            TaxCode = snapshot.Tenant.TaxCode,
            Address = snapshot.Tenant.Address,
            RepresentativeName = snapshot.Tenant.RepresentativeName,
            RepresentativeTitle = snapshot.Tenant.RepresentativeTitle,
            PhoneNumber = snapshot.Tenant.PhoneNumber,
            FaxNumber = snapshot.Tenant.FaxNumber,
            BankAccountNumber = snapshot.Tenant.BankAccountNumber,
            BankName = snapshot.Tenant.BankName,
            LegalNameEn = snapshot.Tenant.LegalNameEn,
            AddressEn = snapshot.Tenant.AddressEn,
            RepresentativeTitleEn = snapshot.Tenant.RepresentativeTitleEn,
            ContactTitleEn = snapshot.Tenant.ContactTitleEn,
            BankNameEn = snapshot.Tenant.BankNameEn,
            CityEn = snapshot.Tenant.CityEn,
            CountryEn = snapshot.Tenant.CountryEn
        });
        legal.Parties.Add(new TblContractVersionPartySnapshot
        {
            PartyRole = 2,
            SourceCustomerId = snapshot.Customer.CustomerId,
            SubjectCode = snapshot.Customer.SubjectCode,
            ContactName = snapshot.Customer.ContactName,
            ContactTitle = snapshot.Customer.ContactTitle,
            ContactPhone = snapshot.Customer.ContactPhone,
            FullName = snapshot.Customer.FullName,
            Email = snapshot.Customer.Email,
            Website = snapshot.Customer.Website,
            City = snapshot.Customer.City,
            Country = snapshot.Customer.Country,
            Mobile = snapshot.Customer.Mobile,
            ZipCode = snapshot.Customer.ZipCode,
            LegalName = snapshot.Customer.LegalName,
            TaxCode = snapshot.Customer.TaxCode,
            Address = snapshot.Customer.Address,
            RepresentativeName = snapshot.Customer.RepresentativeName,
            RepresentativeTitle = snapshot.Customer.RepresentativeTitle,
            PhoneNumber = snapshot.Customer.PhoneNumber,
            FaxNumber = snapshot.Customer.FaxNumber,
            BankAccountNumber = snapshot.Customer.BankAccountNumber,
            BankName = snapshot.Customer.BankName,
            LegalNameEn = snapshot.Customer.LegalNameEn,
            AddressEn = snapshot.Customer.AddressEn,
            RepresentativeTitleEn = snapshot.Customer.RepresentativeTitleEn,
            ContactTitleEn = snapshot.Customer.ContactTitleEn,
            BankNameEn = snapshot.Customer.BankNameEn,
            CityEn = snapshot.Customer.CityEn,
            CountryEn = snapshot.Customer.CountryEn
        });
        foreach (var milestone in snapshot.PaymentMilestones ?? [])
        {
            legal.PaymentMilestones.Add(new TblContractVersionPaymentMilestoneSnapshot
            {
                SourcePaymentMilestoneId = milestone.PaymentMilestoneId,
                SourceTermId = milestone.TermId,
                SourceTemplatePaymentMilestoneId = milestone.SourceTemplatePaymentMilestoneId,
                MilestoneCode = milestone.MilestoneCode,
                TitleVi = milestone.TitleVi,
                TitleEn = milestone.TitleEn,
                PaymentPercent = milestone.PaymentPercent,
                DueAnchor = milestone.DueAnchor,
                DueOffsetDays = milestone.DueOffsetDays,
                DayCountMode = milestone.DayCountMode,
                ConditionVi = milestone.ConditionVi,
                ConditionEn = milestone.ConditionEn,
                DisplayOrder = milestone.DisplayOrder,
                Amount = milestone.Amount,
                AnchorDate = milestone.AnchorDate,
                DueDate = milestone.DueDate,
                PaymentStatus = milestone.PaymentStatus,
                PaidAt = milestone.PaidAt,
                PaidByEmployeeId = milestone.PaidByEmployeeId
            });
        }

        return legal;
    }

    public static string CalculateHash(SoftwareSupplyContractSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        WriteTenant(writer, snapshot.Tenant);
        WriteCustomer(writer, snapshot.Customer);
        WriteString(writer, snapshot.Customer.SubjectCode);
        WriteString(writer, snapshot.Customer.ContactName);
        WriteString(writer, snapshot.Customer.ContactTitle);
        WriteString(writer, snapshot.Customer.ContactPhone);
        WriteString(writer, snapshot.Customer.FullName);
        WriteString(writer, snapshot.Customer.Email);
        WriteString(writer, snapshot.Customer.Website);
        WriteString(writer, snapshot.Customer.City);
        WriteString(writer, snapshot.Customer.Country);
        WriteString(writer, snapshot.Customer.Mobile);
        WriteString(writer, snapshot.Customer.ZipCode);
        WriteString(writer, snapshot.Tenant.LegalNameEn);
        WriteString(writer, snapshot.Tenant.AddressEn);
        WriteString(writer, snapshot.Tenant.RepresentativeTitleEn);
        WriteString(writer, snapshot.Tenant.ContactTitleEn);
        WriteString(writer, snapshot.Tenant.BankNameEn);
        WriteString(writer, snapshot.Tenant.CityEn);
        WriteString(writer, snapshot.Tenant.CountryEn);
        WriteString(writer, snapshot.Customer.LegalNameEn);
        WriteString(writer, snapshot.Customer.AddressEn);
        WriteString(writer, snapshot.Customer.RepresentativeTitleEn);
        WriteString(writer, snapshot.Customer.ContactTitleEn);
        WriteString(writer, snapshot.Customer.BankNameEn);
        WriteString(writer, snapshot.Customer.CityEn);
        WriteString(writer, snapshot.Customer.CountryEn);
        WriteContract(writer, snapshot.Contract);
        WriteVersion(writer, snapshot.Version);

        var items = snapshot.Items.OrderBy(x => x.DisplayOrder).ThenBy(x => x.ContractItemId).ToArray();
        writer.Write(items.Length);
        foreach (var x in items)
        {
            writer.Write(x.ContractItemId); writer.Write(x.ItemType); WriteString(writer, x.ItemCode);
            WriteString(writer, x.ItemName); WriteString(writer, x.ItemNameEn);
            WriteString(writer, x.ItemDescription); WriteString(writer, x.ItemDescriptionEn);
            WriteString(writer, x.UnitName); WriteString(writer, x.UnitNameEn);
            WriteDecimal(writer, x.Quantity); WriteDecimal(writer, x.UnitPrice);
            WriteDecimal(writer, x.LineSubtotal); writer.Write(x.DiscountMode);
            WriteDecimal(writer, x.DiscountPercent); WriteDecimal(writer, x.FixedDiscountAmount);
            WriteDecimal(writer, x.DiscountAmount); writer.Write(x.IsTaxable);
            WriteDecimal(writer, x.VatPercent); WriteDecimal(writer, x.VatAmount);
            WriteDecimal(writer, x.LineTotal); writer.Write(x.DisplayOrder);
        }

        var terms = snapshot.Terms.OrderBy(x => x.DisplayOrder).ThenBy(x => x.TermId).ToArray();
        writer.Write(terms.Length);
        foreach (var x in terms)
        {
            writer.Write(x.TermId); WriteString(writer, x.TermCode); WriteString(writer, x.TermTitle);
            WriteString(writer, x.TermTitleEn); WriteString(writer, x.TermContent);
            WriteString(writer, x.TermContentEn); writer.Write(x.BilingualContentMode); writer.Write(x.IsNegotiable);
            writer.Write(x.DisplayOrder); writer.Write(x.TermKind);
        }

        var placeholders = (snapshot.PlaceholderValues ?? new Dictionary<string, string>())
            .OrderBy(x => x.Key, StringComparer.Ordinal).ToArray();
        writer.Write(placeholders.Length);
        foreach (var x in placeholders) { WriteString(writer, x.Key); WriteString(writer, x.Value); }

        var bases = (snapshot.LegalBases ?? []).OrderBy(x => x.DisplayOrder).ThenBy(x => x.LegalBasisId).ToArray();
        writer.Write(bases.Length);
        foreach (var x in bases)
        {
            writer.Write(x.LegalBasisId); WriteString(writer, x.BasisCode);
            WriteString(writer, x.ContentVi); WriteString(writer, x.ContentEn); writer.Write(x.DisplayOrder);
        }

        var milestones = (snapshot.PaymentMilestones ?? []).OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.PaymentMilestoneId).ToArray();
        writer.Write(milestones.Length);
        foreach (var x in milestones)
        {
            writer.Write(x.PaymentMilestoneId); writer.Write(x.TermId); WriteNullableInt(writer, x.SourceTemplatePaymentMilestoneId);
            WriteString(writer, x.MilestoneCode); WriteString(writer, x.TitleVi); WriteString(writer, x.TitleEn);
            WriteDecimal(writer, x.PaymentPercent); writer.Write(x.DueAnchor); writer.Write(x.DueOffsetDays);
            writer.Write(x.DayCountMode); WriteString(writer, x.ConditionVi); WriteString(writer, x.ConditionEn);
            writer.Write(x.DisplayOrder); WriteDecimal(writer, x.Amount); WriteDate(writer, x.AnchorDate);
            WriteDate(writer, x.DueDate); writer.Write(x.PaymentStatus); WriteDate(writer, x.PaidAt);
            WriteNullableInt(writer, x.PaidByEmployeeId);
        }

        var appendices = (snapshot.Appendices ?? [])
            .OrderBy(x => x.DisplayOrder).ThenBy(x => x.AppendixId).ToArray();
        writer.Write(appendices.Length);
        foreach (var appendix in appendices)
        {
            writer.Write(appendix.AppendixId);
            WriteNullableInt(writer, appendix.SourceTemplateAppendixId);
            WriteString(writer, appendix.AppendixCode);
            WriteString(writer, appendix.AppendixName);
            WriteString(writer, appendix.AppendixNameEn);
            WriteString(writer, appendix.AppendixDescription);
            writer.Write(appendix.IsRequired);
            writer.Write(appendix.DisplayOrder);
            var appendixTerms = appendix.Terms.OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.AppendixTermId).ToArray();
            writer.Write(appendixTerms.Length);
            foreach (var term in appendixTerms)
            {
                writer.Write(term.AppendixTermId);
                WriteNullableInt(writer, term.SourceTemplateAppendixTermId);
                WriteString(writer, term.TermCode);
                WriteString(writer, term.TermTitle);
                WriteString(writer, term.TermTitleEn);
                WriteString(writer, term.TermContent);
                WriteString(writer, term.TermContentEn); writer.Write(term.BilingualContentMode);
                writer.Write(term.DisplayOrder);
            }
        }

        writer.Flush();
        return Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
    }

    private static void WriteTenant(BinaryWriter w, TenantLegalSnapshot x)
    { WriteString(w, x.LegalEntityName); WriteString(w, x.TaxCode); WriteString(w, x.Address); WriteString(w, x.RepresentativeName); WriteString(w, x.RepresentativeTitle); WriteString(w, x.PhoneNumber); WriteString(w, x.FaxNumber); WriteString(w, x.BankAccountNumber); WriteString(w, x.BankName); }
    private static void WriteCustomer(BinaryWriter w, CustomerLegalSnapshot x)
    { w.Write(x.CustomerId); WriteString(w, x.LegalName); WriteString(w, x.TaxCode); WriteString(w, x.Address); WriteString(w, x.RepresentativeName); WriteString(w, x.RepresentativeTitle); WriteString(w, x.PhoneNumber); WriteString(w, x.FaxNumber); WriteString(w, x.BankAccountNumber); WriteString(w, x.BankName); }
    private static void WriteContract(BinaryWriter w, ContractLegalSnapshot x)
    { w.Write(x.ContractId); WriteString(w, x.ContractCode); WriteString(w, x.ContractName); WriteString(w, x.ContractNameEn); w.Write(x.ContractType); w.Write(x.ContractDirection); w.Write(x.TemplateVersionId); WriteDate(w, x.CreatedDate); WriteDate(w, x.SignDate); WriteDate(w, x.EffectiveDate); WriteDate(w, x.ExpireDate); WriteString(w, x.CurrencyCode); w.Write(x.LanguageMode); WriteDecimal(w, x.Subtotal); WriteDecimal(w, x.TotalDiscount); WriteDecimal(w, x.TotalVat); WriteDecimal(w, x.TotalAmount); }
    private static void WriteVersion(BinaryWriter w, ContractVersionLegalSnapshot x)
    { w.Write(x.VersionId); w.Write(x.VersionNo); WriteNullableInt(w, x.SourceVersionId); w.Write(x.TemplateVersionId); WriteString(w, x.CurrencyCode); WriteDecimal(w, x.Subtotal); WriteDecimal(w, x.TotalDiscount); WriteDecimal(w, x.TotalVat); WriteDecimal(w, x.TotalAmount); }
    private static void WriteString(BinaryWriter w, string? value)
    { if (value is null) { w.Write(-1); return; } var bytes = Encoding.UTF8.GetBytes(value); w.Write(bytes.Length); w.Write(bytes); }
    private static void WriteDecimal(BinaryWriter w, decimal value)
    { foreach (var part in decimal.GetBits(value)) w.Write(part); }
    private static void WriteDate(BinaryWriter w, DateTime value) { w.Write(value.ToBinary()); }
    private static void WriteDate(BinaryWriter w, DateTime? value)
    { w.Write(value.HasValue); if (value.HasValue) WriteDate(w, value.Value); }
    private static void WriteNullableInt(BinaryWriter w, int? value)
    { w.Write(value.HasValue); if (value.HasValue) w.Write(value.Value); }

    private static string FirstRequired(
        string? preferred,
        string? fallback,
        string fieldName)
    {
        return !string.IsNullOrWhiteSpace(preferred)
            ? preferred.Trim()
            : Required(fallback, fieldName);
    }

    private static string Required(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{fieldName} chưa được cấu hình.");
        }

        return value.Trim();
    }

    private static string? FirstOptional(string? preferred, string? fallback)
    {
        return Optional(preferred) ?? Optional(fallback);
    }

    private static string? Optional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}
