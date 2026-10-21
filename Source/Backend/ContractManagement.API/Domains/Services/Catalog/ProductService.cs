using ContractManagement.API.Common.Responses;
using ContractManagement.API.Domains.DTOs.Requests.Catalog;
using ContractManagement.API.Common.Enums;
using ContractManagement.API.Common.Exceptions;
using ContractManagement.API.Domains.DTOs.Responses.Catalog;
using ContractManagement.API.Domains.Interfaces.Catalog;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;

using ContractManagement.Domains.Policies.Catalog;

namespace ContractManagement.Domains.Services.Catalog
{
    /// <summary>
    /// Service xử lý nghiệp vụ sản phẩm.
    /// Lưu ý:
    /// - DbDtctechContext đã trỏ đúng tenant DB.
    /// - DB không dùng FK, nên service tự check Category tồn tại.
    /// </summary>
    public class ProductService : IProductService
    {
        private readonly DbDtctechContext _dbContext;

        public ProductService(DbDtctechContext dbContext)
        {
            _dbContext = dbContext;
        }


        public async Task<PagedResult<ProductResponse>> GetListAsync(ProductFilterRequest filter)
        {
            if (filter.Page <= 0) filter.Page = 1;
            if (filter.PageSize <= 0) filter.PageSize = 20;

            var query = _dbContext.TblProducts.AsNoTracking();
            if (filter.Status != CatalogItemStatuses.Deleted)
            {
                query = query.Where(x => x.Status != CatalogItemStatuses.Deleted);
            }

            if (!string.IsNullOrWhiteSpace(filter.Keyword))
            {
                var keyword = filter.Keyword.Trim();

                query = query.Where(x =>
                    (x.ProductCode != null && x.ProductCode.Contains(keyword)) ||
                    (x.ProductName != null && x.ProductName.Contains(keyword)) ||
                    (x.ProductNameEn != null && x.ProductNameEn.Contains(keyword)) ||
                    (x.ProductShortDesc != null && x.ProductShortDesc.Contains(keyword)));
            }

            if (filter.CategoryId.HasValue)
            {
                query = query.Where(x => x.CategoryId == filter.CategoryId.Value);
            }

            if (filter.Status.HasValue)
            {
                query = query.Where(x => x.Status == filter.Status.Value);
            }

            // Apply date filters
            query = ApplyDateFilters(query, filter.FromDate, filter.ToDate);

            var totalCount = await query.CountAsync();

            var products = await query
                .OrderByDescending(x => x.ProductId)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            var categoryNames = await GetCategoryNamesAsync(products);

            return new PagedResult<ProductResponse>
            {
                Items = products
                    .Select(x => MapToResponse(
                        x,
                        x.CategoryId.HasValue &&
                        categoryNames.TryGetValue(x.CategoryId.Value, out var name)
                            ? name
                            : null))
                    .ToList(),

                TotalCount = totalCount,
                Page = filter.Page,
                PageSize = filter.PageSize
            };
        }

        public async Task<ProductResponse> GetByIdAsync(int id, bool includeDeleted = false)
        {
            var product = await _dbContext.TblProducts
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ProductId == id
                    && (includeDeleted || x.Status != CatalogItemStatuses.Deleted));

            if (product == null)
            {
                throw new KeyNotFoundException("Không tìm thấy sản phẩm.");
            }

            string? categoryName = null;

            if (product.CategoryId.HasValue)
            {
                categoryName = await GetCategoryNameAsync(product.CategoryId.Value);
            }

            return MapToResponse(product, categoryName);
        }

        public async Task<ProductResponse> CreateAsync(CreateProductRequest request)
        {
            await ValidateCategoryAsync(request.CategoryId, required: true);

            await ValidateProductCodeAsync(request.ProductCode);

            var englishName = CatalogEnglishNamePolicy.Require(request.ProductNameEn, "Tên sản phẩm tiếng Anh");

            var product = new TblProduct
            {
                ProductCode = request.ProductCode?.Trim(),
                ProductName = request.ProductName.Trim(),
                ProductNameEn = englishName,
                UnitName = CatalogUnitPolicy.Require(request.UnitName, "Đơn vị sản phẩm"),
                UnitNameEn = CatalogUnitPolicy.Require(request.UnitNameEn, "Đơn vị sản phẩm tiếng Anh"),
                CategoryId = request.CategoryId,
                ProductShortDesc = request.ProductShortDesc?.Trim(),
                ProductDetails = request.ProductDetails?.Trim(),
                ProductFeatures = request.ProductFeatures?.Trim(),
                ProductBenefit = request.ProductBenefit?.Trim(),
                ProductPrice = request.ProductPrice,
                ProductSmallImage = request.ProductSmallImage?.Trim(),
                ProductLargeImage = request.ProductLargeImage?.Trim(),
                LangId = request.LangId,
                ProductOrder = request.ProductOrder,
                ProductTags = request.ProductTags?.Trim(),
                TitleBrowser = request.TitleBrowser?.Trim(),
                MetaKeyword = request.MetaKeyword?.Trim(),
                MetaDescription = request.MetaDescription?.Trim(),

                // Quy ước: 1 = Active, 0 = Inactive
                Status = CatalogItemStatuses.Active,

                ProductCreatedDate = DateTime.Now
            };

            _dbContext.TblProducts.Add(product);
            await _dbContext.SaveChangesAsync();

            return await GetByIdAsync(product.ProductId);
        }

        public async Task UpdateAsync(int id, UpdateProductRequest request)
        {
            var product = await _dbContext.TblProducts
                .FirstOrDefaultAsync(x => x.ProductId == id
                    && x.Status != CatalogItemStatuses.Deleted);

            if (product == null)
            {
                throw new KeyNotFoundException("Không tìm thấy sản phẩm.");
            }

            await ValidateCategoryAsync(request.CategoryId, required: true);

            await ValidateProductCodeAsync(request.ProductCode, id);

            var englishName = CatalogEnglishNamePolicy.Require(request.ProductNameEn, "Tên sản phẩm tiếng Anh");

            var unitName = CatalogUnitPolicy.Require(request.UnitName, "Đơn vị sản phẩm");
            var unitNameEn = CatalogUnitPolicy.Require(request.UnitNameEn, "Đơn vị sản phẩm tiếng Anh");
            product.ProductCode = request.ProductCode?.Trim();
            product.ProductName = request.ProductName.Trim();
            product.ProductNameEn = englishName;
            product.UnitName = unitName;
            product.UnitNameEn = unitNameEn;
            product.CategoryId = request.CategoryId;
            product.ProductShortDesc = request.ProductShortDesc?.Trim();
            product.ProductDetails = request.ProductDetails?.Trim();
            product.ProductFeatures = request.ProductFeatures?.Trim();
            product.ProductBenefit = request.ProductBenefit?.Trim();
            product.ProductPrice = request.ProductPrice;
            product.ProductSmallImage = request.ProductSmallImage?.Trim();
            product.ProductLargeImage = request.ProductLargeImage?.Trim();
            product.LangId = request.LangId;
            product.ProductOrder = request.ProductOrder;
            product.ProductTags = request.ProductTags?.Trim();
            product.TitleBrowser = request.TitleBrowser?.Trim();
            product.MetaKeyword = request.MetaKeyword?.Trim();
            product.MetaDescription = request.MetaDescription?.Trim();

            await _dbContext.SaveChangesAsync();
        }

        public async Task SetStatusAsync(int id, byte status)
        {
            if (status is not 0 and not 1)
            {
                throw new ArgumentException("Trạng thái sản phẩm không hợp lệ. Chỉ nhận 0 hoặc 1.");
            }

            var product = await _dbContext.TblProducts
                .FirstOrDefaultAsync(x => x.ProductId == id
                    && x.Status != CatalogItemStatuses.Deleted);

            if (product == null)
            {
                throw new KeyNotFoundException("Không tìm thấy sản phẩm.");
            }

            product.Status = status;

            await _dbContext.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            var product = await _dbContext.TblProducts
                .FirstOrDefaultAsync(x => x.ProductId == id);

            if (product == null)
            {
                throw new KeyNotFoundException("Không tìm thấy sản phẩm.");
            }

            // Hide from the catalog without removing contract, quotation,
            // order or delivery references. Locking remains Status = 0.
            product.Status = CatalogItemStatuses.Deleted;

            await _dbContext.SaveChangesAsync();
        }

        public async Task RestoreAsync(int id)
        {
            var product = await _dbContext.TblProducts.FirstOrDefaultAsync(x => x.ProductId == id);
            if (product == null)
            {
                throw new KeyNotFoundException("Không tìm thấy sản phẩm.");
            }
            if (product.Status != CatalogItemStatuses.Deleted)
            {
                throw new InvalidOperationException("Chỉ có thể khôi phục sản phẩm đã xóa.");
            }

            await ValidateCategoryAsync(product.CategoryId);
            product.Status = CatalogItemStatuses.Inactive;
            await _dbContext.SaveChangesAsync();
        }

        private async Task ValidateProductCodeAsync(string? productCode, int? currentProductId = null)
        {
            if (string.IsNullOrWhiteSpace(productCode)) return;
            var code = productCode.Trim();
            var existing = await _dbContext.TblProducts.AsNoTracking()
                .Where(x => x.ProductCode == code && (!currentProductId.HasValue || x.ProductId != currentProductId.Value))
                .Select(x => new { x.ProductName, x.Status })
                .FirstOrDefaultAsync();
            if (existing == null) return;
            if (existing.Status == CatalogItemStatuses.Deleted)
            {
                throw new BusinessRuleException(StatusCodes.Status409Conflict, "ProductCodeDeleted",
                    $"Mã {code} thuộc sản phẩm \"{existing.ProductName}\" đã xóa. Hãy mở danh sách Đã xóa để khôi phục sản phẩm hoặc sử dụng mã khác.");
            }
            throw new InvalidOperationException("Mã sản phẩm đã tồn tại.");
        }

        private async Task ValidateCategoryAsync(int? categoryId, bool required = false)
        {
            if (!categoryId.HasValue)
            {
                if (required)
                    throw new BusinessRuleException(StatusCodes.Status400BadRequest, "ProductCategoryRequired",
                        "Vui lòng chọn danh mục sản phẩm.");
                return;
            }

            if ((required && categoryId.Value <= 0) || categoryId.Value < byte.MinValue || categoryId.Value > byte.MaxValue)
            {
                throw new ArgumentException("CategoryId không hợp lệ.");
            }

            var categoryExists = await _dbContext.TblCategories
                .AnyAsync(x => x.CategoryId == (byte)categoryId.Value);

            if (!categoryExists)
            {
                throw new KeyNotFoundException("Danh mục không tồn tại.");
            }
        }

        private async Task<string?> GetCategoryNameAsync(int categoryId)
        {
            if (categoryId < byte.MinValue || categoryId > byte.MaxValue)
            {
                return null;
            }

            return await _dbContext.TblCategories
                .AsNoTracking()
                .Where(x => x.CategoryId == (byte)categoryId)
                .Select(x => x.CategoryName)
                .FirstOrDefaultAsync();
        }

        private async Task<Dictionary<int, string>> GetCategoryNamesAsync(List<TblProduct> products)
        {
            var categoryIds = products
                .Where(x =>
                    x.CategoryId.HasValue &&
                    x.CategoryId.Value >= byte.MinValue &&
                    x.CategoryId.Value <= byte.MaxValue)
                .Select(x => (byte)x.CategoryId!.Value)
                .Distinct()
                .ToList();

            if (categoryIds.Count == 0)
            {
                return new Dictionary<int, string>();
            }

            var categories = await _dbContext.TblCategories
                .AsNoTracking()
                .Where(x => categoryIds.Contains(x.CategoryId))
                .ToListAsync();

            return categories.ToDictionary(
                x => (int)x.CategoryId,
                x => x.CategoryName ?? string.Empty);
        }

        private IQueryable<TblProduct> ApplyDateFilters(IQueryable<TblProduct> query, DateTime? fromDate, DateTime? toDate)
        {
            if (!fromDate.HasValue && !toDate.HasValue)
            {
                return query;
            }

            var from = fromDate?.Date ?? DateTime.MinValue;
            var to = toDate?.Date.AddDays(1) ?? DateTime.MaxValue;

            return query.Where(x => x.ProductCreatedDate >= from && x.ProductCreatedDate < to);
        }

        private static ProductResponse MapToResponse(
            TblProduct product,
            string? categoryName)
        {
            return new ProductResponse
            {
                ProductId = product.ProductId,
                ProductCode = product.ProductCode,
                ProductName = product.ProductName,
                ProductNameEn = product.ProductNameEn,
                UnitName = product.UnitName,
                UnitNameEn = product.UnitNameEn,
                CategoryId = product.CategoryId,
                CategoryName = categoryName,
                ProductShortDesc = product.ProductShortDesc,
                ProductDetails = product.ProductDetails,
                ProductFeatures = product.ProductFeatures,
                ProductBenefit = product.ProductBenefit,
                ProductPrice = product.ProductPrice,
                ProductSmallImage = product.ProductSmallImage,
                ProductLargeImage = product.ProductLargeImage,
                LangId = product.LangId,
                Status = product.Status,
                ProductOrder = product.ProductOrder,
                ProductTags = product.ProductTags,
                TitleBrowser = product.TitleBrowser,
                MetaKeyword = product.MetaKeyword,
                MetaDescription = product.MetaDescription,
                ProductCreatedDate = product.ProductCreatedDate
            };
        }
    }
}
