using ContractManagement.API.Common.Responses;
using ContractManagement.API.Common.Exceptions;
using ContractManagement.API.Domains.DTOs.Requests.Customer;
using ContractManagement.API.Domains.DTOs.Responses.Customer;
using ContractManagement.API.Domains.Interfaces.Customer;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.API.Domains.Services.Customer
{
    /// <summary>
    /// Service xử lý nghiệp vụ khách hàng.
    /// - DbDtctechContext đã trỏ đúng tenant DB hiện tại.
    /// - Không dùng DefaultConnection.
    /// - Không trả CustomerPassword ra ngoài API.
    /// </summary>
    public class CustomerService : ICustomerService
    {
        private static string? NormalizeEnglish(string? value, int maxLength, string field)
        {
            var text = value?.Trim();
            if (string.IsNullOrEmpty(text)) return null;
            if (text.Length > maxLength) throw new ArgumentException($"{field} vượt quá {maxLength} ký tự.");
            return text;
        }

        private readonly DbDtctechContext _dbContext;

        public CustomerService(DbDtctechContext dbContext)
        {
            _dbContext = dbContext;
        }

        private IQueryable<TblCustomer> ApplyDateFilter(IQueryable<TblCustomer> query, DateTime? fromDate, DateTime? toDate)
        {
            if (!fromDate.HasValue && !toDate.HasValue) return query;

            var from = fromDate?.Date ?? DateTime.MinValue;
            var to = toDate?.Date.AddDays(1) ?? DateTime.MaxValue;

            return query.Where(x => x.DateCreated >= from && x.DateCreated < to);
        }

        public async Task<PagedResult<CustomerResponse>> GetListAsync(
            CustomerFilterRequest filter)
        {
            if (filter.Page <= 0) filter.Page = 1;
            if (filter.PageSize <= 0) filter.PageSize = 20;

            var query = _dbContext.TblCustomers.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(filter.Keyword))
            {
                var keyword = filter.Keyword.Trim();

                query = query.Where(x =>
                    (x.CustomerFullName != null && x.CustomerFullName.Contains(keyword)) ||
                    (x.CustomerCode != null && x.CustomerCode.Contains(keyword)) ||
                    (x.CustomerCompany != null && x.CustomerCompany.Contains(keyword)) ||
                    (x.CustomerEmail != null && x.CustomerEmail.Contains(keyword)) ||
                    (x.CustomerMobile != null && x.CustomerMobile.Contains(keyword)) ||
                    (x.CustomerContactPersonName != null && x.CustomerContactPersonName.Contains(keyword)) ||
                    (x.CustomerContactPersonPhone != null && x.CustomerContactPersonPhone.Contains(keyword)) ||
                    (x.CustomerTaxCode != null && x.CustomerTaxCode.Contains(keyword)));
            }

            if (filter.Status.HasValue)
            {
                query = query.Where(x => x.Status == filter.Status.Value);
            }

            query = ApplyDateFilter(query, filter.FromDate, filter.ToDate);

            var totalCount = await query.CountAsync();

            var customers = await query
                .OrderByDescending(x => x.CustomerId)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            var contractCounts = await GetContractCountsAsync(customers);

            return new PagedResult<CustomerResponse>
            {
                Items = customers
                    .Select(x => MapToResponse(
                        x,
                        contractCounts.TryGetValue(x.CustomerId, out var count)
                            ? count
                            : 0))
                    .ToList(),

                TotalCount = totalCount,
                Page = filter.Page,
                PageSize = filter.PageSize
            };
        }

        public async Task<IReadOnlyList<CustomerLookupResponse>> GetLookupAsync(
            string? keyword,
            CancellationToken cancellationToken = default)
        {
            var query = _dbContext.TblCustomers.AsNoTracking();
            var normalizedKeyword = keyword?.Trim();

            if (!string.IsNullOrWhiteSpace(normalizedKeyword))
            {
                query = query.Where(x =>
                    (x.CustomerCode != null && x.CustomerCode.Contains(normalizedKeyword))
                    || (x.CustomerFullName != null && x.CustomerFullName.Contains(normalizedKeyword))
                    || (x.CustomerCompany != null && x.CustomerCompany.Contains(normalizedKeyword))
                    || (x.CustomerTaxCode != null && x.CustomerTaxCode.Contains(normalizedKeyword))
                    || (x.CustomerMobile != null && x.CustomerMobile.Contains(normalizedKeyword))
                    || (x.CustomerPhone != null && x.CustomerPhone.Contains(normalizedKeyword))
                    || (x.CustomerContactPersonName != null && x.CustomerContactPersonName.Contains(normalizedKeyword))
                    || (x.CustomerContactPersonPhone != null && x.CustomerContactPersonPhone.Contains(normalizedKeyword)));
            }

            return await query
                .OrderBy(x => x.CustomerFullName ?? x.CustomerCompany ?? x.CustomerCode)
                .ThenBy(x => x.CustomerId)
                .Take(100)
                .Select(x => new CustomerLookupResponse
                {
                    CustomerId = x.CustomerId,
                    CustomerCode = x.CustomerCode,
                    CustomerFullName = x.CustomerFullName,
                    CustomerCompany = x.CustomerCompany,
                    CustomerTaxCode = x.CustomerTaxCode,
                    CustomerMobile = x.CustomerMobile,
                    CustomerPhone = x.CustomerPhone,
                    CustomerContactPersonName = x.CustomerContactPersonName,
                    CustomerContactPersonPhone = x.CustomerContactPersonPhone,
                    CustomerContactPersonTitle = x.CustomerContactPersonTitle,
                    Status = x.Status
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<CustomerResponse> GetByIdAsync(int id)
        {
            var customer = await _dbContext.TblCustomers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.CustomerId == id);

            if (customer == null)
            {
                throw new KeyNotFoundException("Không tìm thấy khách hàng.");
            }

            var totalContracts = await _dbContext.TblContracts
                .AsNoTracking()
                .CountAsync(x => x.CustomerId == id);

            return MapToResponse(customer, totalContracts);
        }

        public Task<CustomerResponse> CreateAsync(
            CreateCustomerRequest request,
            int createdBy)
        {
            return _dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
                () => CreateWithGeneratedCodeAsync(request, createdBy));
        }

        private async Task<CustomerResponse> CreateWithGeneratedCodeAsync(
            CreateCustomerRequest request, int createdBy)
        {
            // Both saves are atomic: never expose a successfully created customer without a code.
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();
            var customer = new TblCustomer
            {
                CustomerFullName = request.CustomerFullName.Trim(),
                CustomerCompany = request.CustomerCompany?.Trim(),
                CustomerCompanyEn = NormalizeEnglish(request.CustomerCompanyEn, 500, "CustomerCompanyEn"),
                CustomerAddressEn = NormalizeEnglish(request.CustomerAddressEn, 500, "CustomerAddressEn"),
                CustomerBankNameEn = NormalizeEnglish(request.CustomerBankNameEn, 500, "CustomerBankNameEn"),
                CustomerCityEn = NormalizeEnglish(request.CustomerCityEn, 100, "CustomerCityEn"),
                CustomerCountryEn = NormalizeEnglish(request.CustomerCountryEn, 100, "CustomerCountryEn"),
                CustomerContactPersonTitleEn = NormalizeEnglish(request.CustomerContactPersonTitleEn, 200, "CustomerContactPersonTitleEn"),
                CustomerRepresentativeTitleEn = NormalizeEnglish(request.CustomerRepresentativeTitleEn, 200, "CustomerRepresentativeTitleEn"),
                CustomerEmail = request.CustomerEmail?.Trim(),
                CustomerMobile = request.CustomerMobile?.Trim(),
                CustomerPhone = request.CustomerPhone?.Trim(),
                CustomerFaxNumber = request.CustomerFaxNumber?.Trim(),
                CustomerTaxCode = request.CustomerTaxCode?.Trim(),
                CustomerRepresentativeName = request.CustomerRepresentativeName?.Trim(),
                CustomerRepresentativeTitle = request.CustomerRepresentativeTitle?.Trim(),
                CustomerContactPersonName = request.CustomerContactPersonName?.Trim(),
                CustomerContactPersonPhone = request.CustomerContactPersonPhone?.Trim(),
                CustomerContactPersonTitle = request.CustomerContactPersonTitle?.Trim(),
                CustomerBankAccountNumber = request.CustomerBankAccountNumber?.Trim(),
                CustomerBankName = request.CustomerBankName?.Trim(),
                CustomerAddress = request.CustomerAddress?.Trim(),
                CustomerCity = request.CustomerCity?.Trim(),
                CustomerCountry = request.CustomerCountry?.Trim(),
                CustomerWebsite = request.CustomerWebsite?.Trim(),
                CustomerNotes = request.CustomerNotes?.Trim(),

                UserCreated = createdBy,
                DateCreated = DateTime.Now,

                // Quy ước hệ thống mới: 1 = Active, 0 = Inactive
                Status = 1
            };

            try
            {
                _dbContext.TblCustomers.Add(customer);
                await _dbContext.SaveChangesAsync();
                customer.CustomerCode = await GenerateCodeAsync(customer.CustomerId);
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
                return MapToResponse(customer, 0);
            }
            catch
            {
                // A transient SQL failure can retry with a fresh identity inside a new transaction.
                _dbContext.Entry(customer).State = EntityState.Detached;
                throw;
            }
        }

        public async Task UpdateAsync(
            int id,
            UpdateCustomerRequest request,
            int updatedBy)
        {
            var customer = await _dbContext.TblCustomers
                .FirstOrDefaultAsync(x => x.CustomerId == id);

            if (customer == null)
            {
                throw new KeyNotFoundException("Không tìm thấy khách hàng.");
            }

            // The code is system-owned and stable across profile edits.
            if (string.IsNullOrWhiteSpace(customer.CustomerCode))
                customer.CustomerCode = await GenerateCodeAsync(customer.CustomerId);
            customer.CustomerFullName = request.CustomerFullName.Trim();
            customer.CustomerCompany = request.CustomerCompany?.Trim();
            customer.CustomerCompanyEn = NormalizeEnglish(request.CustomerCompanyEn, 500, "CustomerCompanyEn");
            customer.CustomerAddressEn = NormalizeEnglish(request.CustomerAddressEn, 500, "CustomerAddressEn");
            customer.CustomerBankNameEn = NormalizeEnglish(request.CustomerBankNameEn, 500, "CustomerBankNameEn");
            customer.CustomerCityEn = NormalizeEnglish(request.CustomerCityEn, 100, "CustomerCityEn");
            customer.CustomerCountryEn = NormalizeEnglish(request.CustomerCountryEn, 100, "CustomerCountryEn");
            customer.CustomerContactPersonTitleEn = NormalizeEnglish(request.CustomerContactPersonTitleEn, 200, "CustomerContactPersonTitleEn");
            customer.CustomerRepresentativeTitleEn = NormalizeEnglish(request.CustomerRepresentativeTitleEn, 200, "CustomerRepresentativeTitleEn");
            customer.CustomerEmail = request.CustomerEmail?.Trim();
            customer.CustomerMobile = request.CustomerMobile?.Trim();
            customer.CustomerPhone = request.CustomerPhone?.Trim();
            customer.CustomerFaxNumber = request.CustomerFaxNumber?.Trim();
            customer.CustomerTaxCode = request.CustomerTaxCode?.Trim();
            customer.CustomerRepresentativeName = request.CustomerRepresentativeName?.Trim();
            customer.CustomerRepresentativeTitle = request.CustomerRepresentativeTitle?.Trim();
            customer.CustomerContactPersonName = request.CustomerContactPersonName?.Trim();
            customer.CustomerContactPersonPhone = request.CustomerContactPersonPhone?.Trim();
            customer.CustomerContactPersonTitle = request.CustomerContactPersonTitle?.Trim();
            customer.CustomerBankAccountNumber = request.CustomerBankAccountNumber?.Trim();
            customer.CustomerBankName = request.CustomerBankName?.Trim();
            customer.CustomerAddress = request.CustomerAddress?.Trim();
            customer.CustomerCity = request.CustomerCity?.Trim();
            customer.CustomerCountry = request.CustomerCountry?.Trim();
            customer.CustomerWebsite = request.CustomerWebsite?.Trim();
            customer.CustomerNotes = request.CustomerNotes?.Trim();

            customer.UserModified = updatedBy;
            customer.DateModified = DateTime.Now;

            await _dbContext.SaveChangesAsync();
        }

        private async Task<string> GenerateCodeAsync(int customerId)
        {
            // Identity allocation is concurrency-safe; do not use MAX(code) + 1.
            var code = $"CUST-{customerId:D6}";
            if (await _dbContext.TblCustomers.AsNoTracking().AnyAsync(x =>
                    x.CustomerId != customerId && x.CustomerCode == code))
                throw new BusinessRuleException(409, "CustomerCodeConflict",
                    "Không thể cấp mã khách hàng tự động vì mã đã thuộc về khách hàng khác. Vui lòng liên hệ quản trị viên.");
            return code;
        }

        public async Task SetStatusAsync(int id, byte status)
        {
            if (status is not 0 and not 1)
            {
                throw new ArgumentException("Trạng thái khách hàng không hợp lệ. Chỉ nhận 0 hoặc 1.");
            }

            var customer = await _dbContext.TblCustomers
                .FirstOrDefaultAsync(x => x.CustomerId == id);

            if (customer == null)
            {
                throw new KeyNotFoundException("Không tìm thấy khách hàng.");
            }

            customer.Status = status;
            customer.DateModified = DateTime.Now;

            await _dbContext.SaveChangesAsync();
        }

        private async Task<Dictionary<int, int>> GetContractCountsAsync(
            List<TblCustomer> customers)
        {
            var customerIds = customers
                .Select(x => x.CustomerId)
                .ToList();

            if (customerIds.Count == 0)
            {
                return new Dictionary<int, int>();
            }

            return await _dbContext.TblContracts
                .AsNoTracking()

                /*
                 * TblContract.CustomerId hiện là int bắt buộc,
                 * nên không cần HasValue hoặc Value nữa.
                 */
                .Where(x => customerIds.Contains(x.CustomerId))
                .GroupBy(x => x.CustomerId)
                .Select(group => new { CustomerId = group.Key, Count = group.Count() })
                .ToDictionaryAsync(
                    x => x.CustomerId,
                    x => x.Count);
        }

        private static CustomerResponse MapToResponse(
            TblCustomer customer,
            int totalContracts)
        {
            return new CustomerResponse
            {
                CustomerId = customer.CustomerId,
                CustomerCode = customer.CustomerCode,
                CustomerFullName = customer.CustomerFullName,
                CustomerCompany = customer.CustomerCompany,
                CustomerCompanyEn = customer.CustomerCompanyEn,
                CustomerAddressEn = customer.CustomerAddressEn,
                CustomerBankNameEn = customer.CustomerBankNameEn,
                CustomerCityEn = customer.CustomerCityEn,
                CustomerCountryEn = customer.CustomerCountryEn,
                CustomerContactPersonTitleEn = customer.CustomerContactPersonTitleEn,
                CustomerRepresentativeTitleEn = customer.CustomerRepresentativeTitleEn,
                CustomerEmail = customer.CustomerEmail,
                CustomerMobile = customer.CustomerMobile,
                CustomerPhone = customer.CustomerPhone,
                CustomerFaxNumber = customer.CustomerFaxNumber,
                CustomerTaxCode = customer.CustomerTaxCode,
                CustomerRepresentativeName = customer.CustomerRepresentativeName,
                CustomerRepresentativeTitle = customer.CustomerRepresentativeTitle,
                CustomerContactPersonName = customer.CustomerContactPersonName,
                CustomerContactPersonPhone = customer.CustomerContactPersonPhone,
                CustomerContactPersonTitle = customer.CustomerContactPersonTitle,
                CustomerBankAccountNumber = customer.CustomerBankAccountNumber,
                CustomerBankName = customer.CustomerBankName,
                CustomerAddress = customer.CustomerAddress,
                CustomerCity = customer.CustomerCity,
                CustomerCountry = customer.CustomerCountry,
                CustomerWebsite = customer.CustomerWebsite,
                CustomerNotes = customer.CustomerNotes,
                Status = customer.Status,
                DateCreated = customer.DateCreated,
                DateModified = customer.DateModified,
                TotalContracts = totalContracts
            };
        }
    }
}
