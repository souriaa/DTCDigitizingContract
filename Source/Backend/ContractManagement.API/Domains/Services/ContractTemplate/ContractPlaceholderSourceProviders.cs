using ContractManagement.Domains.Interfaces.ContractTemplate;
using ContractManagement.Domains.Policies.ContractTemplate;

namespace ContractManagement.Domains.Services.ContractTemplate;

// Providers read the preloaded render context; no per-field database queries.
public abstract class ContractPlaceholderSourceProviderBase : IContractPlaceholderSourceProvider
{
    private readonly List<ContractPlaceholderSourceBinding> _bindings = [];
    public IReadOnlyList<ContractPlaceholderSourceBinding> GetBindings() => _bindings;
    protected void Add(string key, string module, string moduleLabel, string label, PlaceholderValueType type,
        Func<ContractPlaceholderResolveContext, object?> read)
    {
        string[] formats = type switch
        {
            PlaceholderValueType.Number => ["N0", "N2", "0.##"],
            PlaceholderValueType.Date => ["dd/MM/yyyy", "yyyy-MM-dd", "dd MMMM yyyy"],
            PlaceholderValueType.DateTime => ["dd/MM/yyyy HH:mm", "yyyy-MM-dd HH:mm"],
            PlaceholderValueType.Boolean => ["Có/Không", "Yes/No"],
            _ => []
        };
        object sample = type switch
        {
            PlaceholderValueType.Number => 1250000.5m,
            PlaceholderValueType.Date or PlaceholderValueType.DateTime => new DateTime(2026, 9, 10, 14, 30, 0),
            PlaceholderValueType.Boolean => true,
            _ => key.EndsWith("-en", StringComparison.Ordinal) ? "Sample English value" : $"{label} mẫu"
        };
        var samples = formats.ToDictionary(f => f, f => ContractPlaceholderSourceRegistry.FormatValue(sample, f), StringComparer.Ordinal);
        _bindings.Add(new(new(key, module, moduleLabel, label, type, true, formats,
            ContractPlaceholderSourceRegistry.FormatValue(sample, formats.FirstOrDefault()), samples), read));
    }

}

public sealed class ContractPlaceholderSourceProvider : ContractPlaceholderSourceProviderBase
{
    public ContractPlaceholderSourceProvider()
    {
        Add("contract.code", "contract", "Hợp đồng", "Mã hợp đồng", PlaceholderValueType.Text, c => c.Contract.ContractCode);
        Add("contract.name", "contract", "Hợp đồng", "Tên hợp đồng", PlaceholderValueType.Text, c => c.Contract.ContractName);
        Add("contract.name-en", "contract", "Hợp đồng", "Tên tiếng Anh", PlaceholderValueType.Text, c => c.Contract.ContractNameEn);
        Add("contract.created-date-en", "contract", "Hợp đồng", "Ngày hợp đồng tiếng Anh", PlaceholderValueType.Text, c => DocumentEnglishPolicy.Date(c.Contract.CreatedDate));
        Add("contract.effective-date-en", "contract", "Hợp đồng", "Ngày hiệu lực tiếng Anh", PlaceholderValueType.Text, c => c.Contract.EffectiveDate is { } date ? DocumentEnglishPolicy.Date(date) : null);
        Add("contract.expire-date-en", "contract", "Hợp đồng", "Ngày hết hạn tiếng Anh", PlaceholderValueType.Text, c => c.Contract.ExpireDate is { } date ? DocumentEnglishPolicy.Date(date) : null);
        Add("contract.created-date", "contract", "Hợp đồng", "Ngày hợp đồng", PlaceholderValueType.Date, c => c.Contract.CreatedDate);
        Add("contract.effective-date", "contract", "Hợp đồng", "Ngày hiệu lực", PlaceholderValueType.Date, c => c.Contract.EffectiveDate);
        Add("contract.expire-date", "contract", "Hợp đồng", "Ngày hết hạn", PlaceholderValueType.Date, c => c.Contract.ExpireDate);
        Add("contract.sign-date", "contract", "Hợp đồng", "Ngày ký", PlaceholderValueType.Date, c => c.Contract.SignDate);
    }
}

public sealed class ContractVersionPlaceholderSourceProvider : ContractPlaceholderSourceProviderBase
{
    public ContractVersionPlaceholderSourceProvider()
    {
        Add("version.total-amount-in-words-en", "version", "Phiên bản hợp đồng", "Tổng giá trị bằng chữ tiếng Anh", PlaceholderValueType.Text, c => EnglishMoneyTextFormatter.Format(c.Version.TotalAmount, c.Version.CurrencyCode));
        Add("version.currency", "version", "Phiên bản hợp đồng", "Tiền tệ", PlaceholderValueType.Text, c => c.Version.CurrencyCode);
        Add("version.total-amount", "version", "Phiên bản hợp đồng", "Tổng giá trị", PlaceholderValueType.Number, c => c.Version.TotalAmount);
        Add("version.subtotal", "version", "Phiên bản hợp đồng", "Thành tiền", PlaceholderValueType.Number, c => c.Version.Subtotal);
        Add("version.total-discount", "version", "Phiên bản hợp đồng", "Tổng giảm giá", PlaceholderValueType.Number, c => c.Version.TotalDiscount);
        Add("version.total-vat", "version", "Phiên bản hợp đồng", "Tổng thuế", PlaceholderValueType.Number, c => c.Version.TotalVat);
        Add("version.number", "version", "Phiên bản hợp đồng", "Số phiên bản", PlaceholderValueType.Number, c => c.Version.VersionNo);
    }
}

public sealed class CustomerPlaceholderSourceProvider : ContractPlaceholderSourceProviderBase
{
    public CustomerPlaceholderSourceProvider()
    {
        Add("counterparty.company-en", "counterparty", "Đối tác ngoài hệ thống", "Tên công ty tiếng Anh", PlaceholderValueType.Text, c => c.Customer.CustomerCompanyEn);
        Add("counterparty.address-en", "counterparty", "Đối tác ngoài hệ thống", "Địa chỉ tiếng Anh", PlaceholderValueType.Text, c => c.Customer.CustomerAddressEn);
        Add("counterparty.bank-name-en", "counterparty", "Đối tác ngoài hệ thống", "Tên ngân hàng tiếng Anh", PlaceholderValueType.Text, c => c.Customer.CustomerBankNameEn);
        Add("counterparty.city-en", "counterparty", "Đối tác ngoài hệ thống", "Tỉnh/thành tiếng Anh", PlaceholderValueType.Text, c => c.Customer.CustomerCityEn);
        Add("counterparty.country-en", "counterparty", "Đối tác ngoài hệ thống", "Quốc gia tiếng Anh", PlaceholderValueType.Text, c => c.Customer.CustomerCountryEn);
        Add("counterparty.contact-title-en", "counterparty", "Đối tác ngoài hệ thống", "Chức danh người liên lạc tiếng Anh", PlaceholderValueType.Text, c => c.Customer.CustomerContactPersonTitleEn);
        Add("counterparty.representative-title-en", "counterparty", "Đối tác ngoài hệ thống", "Chức danh đại diện pháp luật tiếng Anh", PlaceholderValueType.Text, c => c.Customer.CustomerRepresentativeTitleEn);
        Add("counterparty.code", "counterparty", "Đối tác ngoài hệ thống", "Mã khách hàng", PlaceholderValueType.Text, c => c.Customer.CustomerCode);
        Add("counterparty.name", "counterparty", "Đối tác ngoài hệ thống", "Họ tên", PlaceholderValueType.Text, c => c.Customer.CustomerFullName);
        Add("counterparty.company", "counterparty", "Đối tác ngoài hệ thống", "Công ty", PlaceholderValueType.Text, c => c.Customer.CustomerCompany);
        Add("counterparty.email", "counterparty", "Đối tác ngoài hệ thống", "Email", PlaceholderValueType.Text, c => c.Customer.CustomerEmail);
        Add("counterparty.phone", "counterparty", "Đối tác ngoài hệ thống", "Điện thoại", PlaceholderValueType.Text, c => c.Customer.CustomerPhone);
        Add("counterparty.mobile", "counterparty", "Đối tác ngoài hệ thống", "Di động", PlaceholderValueType.Text, c => c.Customer.CustomerMobile);
        Add("counterparty.address", "counterparty", "Đối tác ngoài hệ thống", "Địa chỉ", PlaceholderValueType.Text, c => c.Customer.CustomerAddress);
        Add("counterparty.tax-code", "counterparty", "Đối tác ngoài hệ thống", "Mã số thuế", PlaceholderValueType.Text, c => c.Customer.CustomerTaxCode);
        Add("counterparty.representative-name", "counterparty", "Đối tác ngoài hệ thống", "Đại diện pháp luật", PlaceholderValueType.Text, c => c.Customer.CustomerRepresentativeName);
        Add("counterparty.representative-title", "counterparty", "Đối tác ngoài hệ thống", "Chức danh đại diện", PlaceholderValueType.Text, c => c.Customer.CustomerRepresentativeTitle);
        Add("counterparty.contact-name", "counterparty", "Đối tác ngoài hệ thống", "Người liên hệ", PlaceholderValueType.Text, c => c.Customer.CustomerContactPersonName);
        Add("counterparty.contact-phone", "counterparty", "Đối tác ngoài hệ thống", "Điện thoại liên hệ", PlaceholderValueType.Text, c => c.Customer.CustomerContactPersonPhone);
        Add("counterparty.contact-title", "counterparty", "Đối tác ngoài hệ thống", "Chức danh liên hệ", PlaceholderValueType.Text, c => c.Customer.CustomerContactPersonTitle);
        Add("counterparty.fax", "counterparty", "Đối tác ngoài hệ thống", "Fax", PlaceholderValueType.Text, c => c.Customer.CustomerFaxNumber);
        Add("counterparty.bank-account", "counterparty", "Đối tác ngoài hệ thống", "Số tài khoản", PlaceholderValueType.Text, c => c.Customer.CustomerBankAccountNumber);
        Add("counterparty.bank-name", "counterparty", "Đối tác ngoài hệ thống", "Ngân hàng", PlaceholderValueType.Text, c => c.Customer.CustomerBankName);
        Add("counterparty.website", "counterparty", "Đối tác ngoài hệ thống", "Website", PlaceholderValueType.Text, c => c.Customer.CustomerWebsite);
        Add("counterparty.city", "counterparty", "Đối tác ngoài hệ thống", "Tỉnh/thành", PlaceholderValueType.Text, c => c.Customer.CustomerCity);
        Add("counterparty.country", "counterparty", "Đối tác ngoài hệ thống", "Quốc gia", PlaceholderValueType.Text, c => c.Customer.CustomerCountry);
        Add("counterparty.zip-code", "counterparty", "Đối tác ngoài hệ thống", "Mã bưu chính", PlaceholderValueType.Text, c => c.Customer.CustomerZipCode);
    }
}

public sealed class TenantLegalProfilePlaceholderSourceProvider : ContractPlaceholderSourceProviderBase
{
    public TenantLegalProfilePlaceholderSourceProvider()
    {
        Add("tenant.legal-name-en", "tenant", "Doanh nghiệp sử dụng hệ thống", "Tên pháp nhân tiếng Anh", PlaceholderValueType.Text, c => c.Tenant?.LegalEntityNameEn);
        Add("tenant.address-en", "tenant", "Doanh nghiệp sử dụng hệ thống", "Địa chỉ tiếng Anh", PlaceholderValueType.Text, c => c.Tenant?.AddressEn);
        Add("tenant.representative-title-en", "tenant", "Doanh nghiệp sử dụng hệ thống", "Chức danh đại diện tiếng Anh", PlaceholderValueType.Text, c => c.Tenant?.RepresentativeTitleEn);
        Add("tenant.bank-name-en", "tenant", "Doanh nghiệp sử dụng hệ thống", "Tên ngân hàng tiếng Anh", PlaceholderValueType.Text, c => c.Tenant?.BankNameEn);
        Add("tenant.legal-name", "tenant", "Doanh nghiệp sử dụng hệ thống", "Tên pháp nhân", PlaceholderValueType.Text, c => c.Tenant?.LegalEntityName);
        Add("tenant.tax-code", "tenant", "Doanh nghiệp sử dụng hệ thống", "Mã số thuế", PlaceholderValueType.Text, c => c.Tenant?.TaxCode);
        Add("tenant.address", "tenant", "Doanh nghiệp sử dụng hệ thống", "Địa chỉ", PlaceholderValueType.Text, c => c.Tenant?.Address);
        Add("tenant.representative-name", "tenant", "Doanh nghiệp sử dụng hệ thống", "Đại diện pháp luật", PlaceholderValueType.Text, c => c.Tenant?.RepresentativeName);
        Add("tenant.representative-title", "tenant", "Doanh nghiệp sử dụng hệ thống", "Chức danh đại diện", PlaceholderValueType.Text, c => c.Tenant?.RepresentativeTitle);
        Add("tenant.phone", "tenant", "Doanh nghiệp sử dụng hệ thống", "Điện thoại", PlaceholderValueType.Text, c => c.Tenant?.PhoneNumber);
        Add("tenant.fax", "tenant", "Doanh nghiệp sử dụng hệ thống", "Fax", PlaceholderValueType.Text, c => c.Tenant?.FaxNumber);
        Add("tenant.bank-account", "tenant", "Doanh nghiệp sử dụng hệ thống", "Số tài khoản", PlaceholderValueType.Text, c => c.Tenant?.BankAccountNumber);
        Add("tenant.bank-name", "tenant", "Doanh nghiệp sử dụng hệ thống", "Ngân hàng", PlaceholderValueType.Text, c => c.Tenant?.BankName);
    }
}

public sealed class ContractOwnerPlaceholderSourceProvider : ContractPlaceholderSourceProviderBase
{
    public ContractOwnerPlaceholderSourceProvider()
    {
        Add("contract-owner.full-name", "contract-owner", "Nhân viên phụ trách", "Họ tên", PlaceholderValueType.Text, c => c.Owner?.EmployeeFullName);
        Add("contract-owner.code", "contract-owner", "Nhân viên phụ trách", "Mã nhân viên", PlaceholderValueType.Text, c => c.Owner?.EmployeeCode);
        Add("contract-owner.email", "contract-owner", "Nhân viên phụ trách", "Email", PlaceholderValueType.Text, c => c.Owner?.EmployeeEmail);
        Add("contract-owner.phone", "contract-owner", "Nhân viên phụ trách", "Điện thoại", PlaceholderValueType.Text, c => c.Owner?.EmployeePhone);
        Add("contract-owner.mobile", "contract-owner", "Nhân viên phụ trách", "Di động", PlaceholderValueType.Text, c => c.Owner?.EmployeeMobile);
    }
}

public sealed class DepartmentPlaceholderSourceProvider : ContractPlaceholderSourceProviderBase
{
    public DepartmentPlaceholderSourceProvider()
    {
        Add("department.code", "department", "Phòng ban phụ trách", "Mã phòng ban", PlaceholderValueType.Text, c => c.Department?.DepartmentCode);
        Add("department.name", "department", "Phòng ban phụ trách", "Tên phòng ban", PlaceholderValueType.Text, c => c.Department?.DepartmentName);
    }
}
public sealed class ContractRolePlaceholderSourceProvider : ContractPlaceholderSourceProviderBase
{
    public ContractRolePlaceholderSourceProvider()
    {
        foreach (var role in new[] { "provider", "customer" })
            foreach (var field in DocumentPartyMarkers.Fields)
                Add(role + "." + DocumentPartyMarkers.SourceSuffix(field.Suffix), role,
                    role == "provider" ? "Vai trò: Bên cung cấp" : "Vai trò: Bên mua", field.Label,
                    PlaceholderValueType.Text,
                    context => (role == "provider" ? context.Parties.Provider : context.Parties.Customer).Value.Get(field.Suffix));
        Add("customer.representative-phone", "customer", "Vai trò: Bên mua", "Điện thoại người đại diện tham gia / người liên lạc",
            PlaceholderValueType.Text, context => context.Parties.Customer.Value.Get("REPRESENTATIVE_PHONE"));
    }
}
