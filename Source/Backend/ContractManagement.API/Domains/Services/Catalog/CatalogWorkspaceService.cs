using ContractManagement.API.Common.Enums;
using ContractManagement.API.Common.Exceptions;
using ContractManagement.API.Common.Responses;
using ContractManagement.API.Domains.DTOs.Requests.Catalog;
using ContractManagement.API.Domains.DTOs.Responses.Catalog;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;
namespace ContractManagement.Domains.Services.Catalog;

/// <summary>Read-only tenant-scoped projections. No schema changes or writes.</summary>
public sealed partial class CatalogWorkspaceService(DbDtctechContext db)
{
    private IQueryable<TblProduct> Products(CatalogWorkspaceFilter f)
    {
        f.EnsureValid();
        var q = db.TblProducts.AsNoTracking();
        q = f.Status.HasValue ? q.Where(x => x.Status == f.Status) : q.Where(x => x.Status != CatalogItemStatuses.Deleted);
        if (f.GroupId.HasValue) q = q.Where(x => x.CategoryId == f.GroupId);
        if (f.UnassignedOnly) q = q.Where(x => x.CategoryId == null);
        if (f.FromDate.HasValue) { var from = f.FromDate.Value.Date; q = q.Where(x => x.ProductCreatedDate >= from); }
        if (f.ToDate.HasValue) { var to = f.ToDate.Value.Date.AddDays(1); q = q.Where(x => x.ProductCreatedDate < to); }
        if (!string.IsNullOrWhiteSpace(f.Keyword))
        {
            var key = f.Keyword.Trim();
            q = q.Where(x => (x.ProductCode != null && x.ProductCode.Contains(key))
                || (x.ProductName != null && x.ProductName.Contains(key))
                || (x.ProductNameEn != null && x.ProductNameEn.Contains(key))
                || (x.ProductShortDesc != null && x.ProductShortDesc.Contains(key)));
        }
        return q;
    }
    private IQueryable<TblService> Services(CatalogWorkspaceFilter f)
    {
        f.EnsureValid();
        var q = db.TblServices.AsNoTracking();
        q = f.Status.HasValue ? q.Where(x => x.Status == f.Status) : q.Where(x => x.Status != CatalogItemStatuses.Deleted);
        if (f.GroupId.HasValue) q = q.Where(x => x.ServiceTypeId == f.GroupId);
        if (f.UnassignedOnly) q = q.Where(x => x.ServiceTypeId == null);
        if (f.FromDate.HasValue) { var from = f.FromDate.Value.Date; q = q.Where(x => x.DateCreated >= from); }
        if (f.ToDate.HasValue) { var to = f.ToDate.Value.Date.AddDays(1); q = q.Where(x => x.DateCreated < to); }
        if (!string.IsNullOrWhiteSpace(f.Keyword))
        {
            var key = f.Keyword.Trim();
            q = q.Where(x => (x.ServiceName != null && x.ServiceName.Contains(key))
                || (x.ServiceNameEn != null && x.ServiceNameEn.Contains(key))
                || (x.ServiceShortDesc != null && x.ServiceShortDesc.Contains(key)));
        }
        return q;
    }
    private async Task<List<CatalogGroupNode>> CategoryNodes(CancellationToken ct)
    {
        var nodes = await db.TblCategories.AsNoTracking().OrderBy(x => x.CategoryOrder).ThenBy(x => x.CategoryName)
            .ThenBy(x => x.CategoryId).Select(x => new CatalogGroupNode
            { Id = x.CategoryId, Name = x.CategoryName ?? "Danh mục chưa có tên", ParentId = x.CategoryParentId, Order = x.CategoryOrder })
            .ToListAsync(ct);
        CompleteHierarchy(nodes);
        return nodes;
    }
    public static void CompleteHierarchy(IReadOnlyList<CatalogGroupNode> nodes)
    {
        var map = nodes.ToDictionary(x => x.Id);
        foreach (var node in nodes) { node.SubtreeCount = node.DirectCount; node.DescendantCount = 0; }
        foreach (var node in nodes)
        {
            var names = new List<string> { node.Name };
            var visited = new HashSet<int> { node.Id };
            var parentId = node.ParentId;
            while (parentId.HasValue)
            {
                if (!visited.Add(parentId.Value) || !map.TryGetValue(parentId.Value, out var parent))
                    throw new BusinessRuleException(409, "CategoryHierarchyInvalid", "Cây danh mục có vòng lặp hoặc thiếu danh mục cha. Vui lòng kiểm tra lại danh mục.");
                parent.SubtreeCount += node.DirectCount;
                parent.DescendantCount++;
                names.Add(parent.Name);
                parentId = parent.ParentId;
            }
            names.Reverse();
            node.Path = string.Join(" / ", names);
        }
    }
    public async Task<CatalogGroupsResponse> GetCategoriesAsync(CatalogWorkspaceFilter filter, CancellationToken ct = default)
    {
        var nodes = await CategoryNodes(ct);
        var counts = await Products(filter).GroupBy(x => x.CategoryId)
            .Select(g => new { Id = g.Key, Count = g.Count() }).ToListAsync(ct);
        var map = nodes.ToDictionary(x => x.Id);
        foreach (var count in counts.Where(x => x.Id.HasValue))
        {
            if (!map.TryGetValue(count.Id!.Value, out var node))
                throw new BusinessRuleException(409, "ProductCategoryInvalid", "Có sản phẩm tham chiếu danh mục không tồn tại. Vui lòng kiểm tra danh mục sản phẩm.");
            node.DirectCount = count.Count;
        }
        CompleteHierarchy(nodes);
        return new(nodes, counts.Where(x => x.Id == null).Sum(x => x.Count), counts.Sum(x => x.Count));
    }
    public async Task<CatalogGroupsResponse> GetServiceTypesAsync(CatalogWorkspaceFilter filter, CancellationToken ct = default)
    {
        var nodes = await db.TblServiceTypes.AsNoTracking().OrderBy(x => x.ServiceTypeName).ThenBy(x => x.ServiceTypeId)
            .Select(x => new CatalogGroupNode { Id = x.ServiceTypeId, Name = x.ServiceTypeName ?? "Loại dịch vụ chưa có tên", Path = x.ServiceTypeName ?? "Loại dịch vụ chưa có tên" })
            .ToListAsync(ct);
        var counts = await Services(filter).GroupBy(x => x.ServiceTypeId)
            .Select(g => new { Id = g.Key, Count = g.Count() }).ToListAsync(ct);
        var map = nodes.ToDictionary(x => x.Id);
        foreach (var count in counts.Where(x => x.Id.HasValue))
        {
            if (!map.TryGetValue(count.Id!.Value, out var node))
                throw new BusinessRuleException(409, "ServiceTypeInvalid", "Có dịch vụ tham chiếu loại không tồn tại. Vui lòng kiểm tra loại dịch vụ.");
            node.DirectCount = node.SubtreeCount = count.Count;
        }
        return new(nodes, counts.Where(x => x.Id == null).Sum(x => x.Count), counts.Sum(x => x.Count));
    }
    public async Task<PagedResult<CatalogItemSummaryResponse>> GetProductsAsync(CatalogWorkspaceFilter filter, CancellationToken ct = default)
    {
        var q = Products(filter);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(x => x.ProductId).Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
            .Select(x => new CatalogItemSummaryResponse
            { Id = x.ProductId, Name = x.ProductName, UnitName = x.UnitName, UnitNameEn = x.UnitNameEn, Code = x.ProductCode, GroupId = x.CategoryId, Price = x.ProductPrice, Status = x.Status, CreatedAt = x.ProductCreatedDate })
            .ToListAsync(ct);
        var map = (await CategoryNodes(ct)).ToDictionary(x => x.Id, x => x.Path);
        foreach (var row in rows)
            row.GroupPath = row.GroupId == null ? "Chưa phân danh mục" : map.TryGetValue(row.GroupId.Value, out var path) ? path
                : throw new BusinessRuleException(409, "ProductCategoryInvalid", "Danh mục sản phẩm không tồn tại.");
        return new() { Items = rows, TotalCount = total, Page = filter.Page, PageSize = filter.PageSize };
    }
    public async Task<PagedResult<CatalogItemSummaryResponse>> GetServicesAsync(CatalogWorkspaceFilter filter, CancellationToken ct = default)
    {
        var q = Services(filter);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(x => x.ServiceId).Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
            .Select(x => new CatalogItemSummaryResponse
            { Id = x.ServiceId, Name = x.ServiceName, UnitName = x.UnitName, UnitNameEn = x.UnitNameEn, GroupId = x.ServiceTypeId, ParentId = x.ServiceParentId,
                Price = x.ServicePrice, SetupPrice = x.SetupPrice, MaintainPrice = x.MaintainPrice, Status = x.Status, CreatedAt = x.DateCreated })
            .ToListAsync(ct);
        var typeIds = rows.Where(x => x.GroupId.HasValue).Select(x => x.GroupId!.Value).Distinct().ToArray();
        var types = await db.TblServiceTypes.AsNoTracking().Where(x => typeIds.Contains(x.ServiceTypeId))
            .ToDictionaryAsync(x => (int)x.ServiceTypeId, x => x.ServiceTypeName, ct);
        var parentIds = rows.Where(x => x.ParentId.HasValue).Select(x => x.ParentId!.Value).Distinct().ToArray();
        var parents = await db.TblServices.AsNoTracking().Where(x => parentIds.Contains(x.ServiceId))
            .ToDictionaryAsync(x => x.ServiceId, x => x.ServiceName, ct);
        foreach (var row in rows)
        {
            row.GroupPath = row.GroupId == null ? "Chưa phân loại" : types.TryGetValue(row.GroupId.Value, out var typeName) ? typeName ?? "Loại dịch vụ chưa có tên"
                : throw new BusinessRuleException(409, "ServiceTypeInvalid", "Loại dịch vụ không tồn tại.");
            row.ParentName = row.ParentId == null ? null : parents.TryGetValue(row.ParentId.Value, out var parentName) ? parentName
                : throw new BusinessRuleException(409, "ServiceParentInvalid", "Dịch vụ cha không tồn tại.");
        }
        return new() { Items = rows, TotalCount = total, Page = filter.Page, PageSize = filter.PageSize };
    }
}
