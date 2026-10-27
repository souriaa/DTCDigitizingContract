using ContractManagement.API.Common.Enums;
using ContractManagement.API.Common.Responses;
using ContractManagement.API.Domains.DTOs.Requests.Catalog;
using ContractManagement.API.Domains.DTOs.Responses.Catalog;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Domains.Services.Catalog;

public sealed partial class CatalogWorkspaceService
{
    public async Task<PagedResult<CatalogPickerItemResponse>> GetPickerItemsAsync(CatalogPickerFilter filter, CancellationToken ct = default)
    {
        filter.EnsureValid();
        // This picker is for adding new contract items: locked/deleted sources cannot be selected.
        var common = new CatalogWorkspaceFilter { Keyword = filter.Keyword, Status = CatalogItemStatuses.Active };
        var products = Products(common);
        var services = Services(common);
        if (filter.ItemType == ContractItemType.Product) services = services.Where(_ => false);
        if (filter.ItemType == ContractItemType.Service) products = products.Where(_ => false);
        if (filter.SelectedOnly)
        {
            var productIds = filter.ProductIds?.Distinct().ToArray() ?? [];
            var serviceIds = filter.ServiceIds?.Distinct().ToArray() ?? [];
            products = products.Where(x => productIds.Contains(x.ProductId));
            services = services.Where(x => serviceIds.Contains(x.ServiceId));
        }

        var productRows = products.Select(x => new CatalogPickerItemResponse {
            SourceId = x.ProductId, ItemType = ContractItemType.Product, Name = x.ProductName,
            NameEn = x.ProductNameEn, UnitName = x.UnitName, UnitNameEn = x.UnitNameEn, Price = x.ProductPrice });
        var serviceRows = services.Select(x => new CatalogPickerItemResponse {
            SourceId = x.ServiceId, ItemType = ContractItemType.Service, Name = x.ServiceName,
            NameEn = x.ServiceNameEn, UnitName = x.UnitName, UnitNameEn = x.UnitNameEn, Price = x.ServicePrice });

        CatalogPickerItemResponse? focus = null;
        if (filter.FocusItemType == ContractItemType.Product)
            focus = await productRows.SingleOrDefaultAsync(x => x.SourceId == filter.FocusItemId, ct);
        else if (filter.FocusItemType == ContractItemType.Service)
            focus = await serviceRows.SingleOrDefaultAsync(x => x.SourceId == filter.FocusItemId, ct);
        if (focus is not null)
        {
            if (focus.ItemType == ContractItemType.Product) productRows = productRows.Where(x => x.SourceId != focus.SourceId);
            else serviceRows = serviceRows.Where(x => x.SourceId != focus.SourceId);
        }

        var productCount = await productRows.CountAsync(ct);
        var serviceCount = await serviceRows.CountAsync(ct);
        var skip = (filter.Page - 1) * filter.PageSize;
        var items = new List<CatalogPickerItemResponse>();
        if (focus is not null && skip == 0) items.Add(focus);
        var offset = Math.Max(0, skip - (focus is null ? 0 : 1));
        // Page across the two ordered SQL queries, including pages spanning both types.
        // Only rows belonging to the requested page are loaded, never the entire catalog.
        if (offset < productCount)
            items.AddRange(await productRows.OrderByDescending(x => x.SourceId).Skip(offset)
                .Take(filter.PageSize - items.Count).ToListAsync(ct));
        if (items.Count < filter.PageSize)
            items.AddRange(await serviceRows.OrderByDescending(x => x.SourceId).Skip(Math.Max(0, offset - productCount))
                .Take(filter.PageSize - items.Count).ToListAsync(ct));

        return new() { Items = items, TotalCount = productCount + serviceCount + (focus is null ? 0 : 1),
            Page = filter.Page, PageSize = filter.PageSize };
    }
}
