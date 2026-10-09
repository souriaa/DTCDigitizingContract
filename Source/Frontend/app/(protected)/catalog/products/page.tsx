"use client";

import { useState, useEffect, useMemo, useCallback, useRef } from "react";
import { Header } from "@/components/ui/custom/header";
import {
  Package,
  CheckCircle2,
  Trash2,
  Lock,
  Unlock,
  Eye,
  UserCog,
  Plus,
  Layers,
  RotateCcw,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { toast } from "@/components/ui/sonner";
import { ColumnDef, PaginationState } from "@tanstack/react-table";
import { format } from "date-fns";

import {
  productApi,
  ProductResponse,
  ProductStatus,
  getProductStatusLabel,
} from "@/services/catalog/products-api";
import { DataTable } from "@/components/ui/custom/data-table-server";
import { StatusBadge } from "@/components/ui/custom/status-badge";
import { ConfirmDialog } from "@/components/ui/custom/confirm-dialog";
import { getApiErrorMessage } from "@/lib/api-error";
import { showConfirmToast } from "@/components/ui/custom/confirm-toast";
import { SplitActionMenu } from "@/components/ui/custom/split-action-menu";
import { SelectFilter } from "@/components/ui/custom/select-filter";
import {
  DateRangeFilter,
  DateRange,
} from "@/components/ui/custom/date-range-filter";
import {
  SummaryCardItem,
  SummaryCards,
} from "@/components/ui/custom/summary-cards";
import { PageHeaderSkeleton } from "@/components/ui/custom/table-skeleton";
import { ProductFormModal } from "./product-form-modal";
import { usePermission } from "@/hooks/use-permission";
import { RBAC_PERMISSIONS } from "@/lib/rbac";
import { Badge } from "@/components/ui/badge";

function ProductBulkActions({
  selectedRows,
  resetSelection,
  onChanged,
}: {
  selectedRows: ProductResponse[];
  resetSelection: () => void;
  onChanged: () => void;
}) {
  const [isLoading, setIsLoading] = useState(false);

  const handleBulkSetStatus = async (status: ProductStatus) => {
    setIsLoading(true);
    try {
      await Promise.all(
        selectedRows.map((product) =>
          productApi.setStatus(product.productId, status),
        ),
      );
      toast.success(
        `Đã cập nhật trạng thái cho ${selectedRows.length} sản phẩm`,
      );
      resetSelection();
    } catch (error) {
      toast.error("Lỗi khi cập nhật trạng thái hàng loạt");
    } finally {
      setIsLoading(false);
      onChanged();
    }
  };

  const handleBulkDelete = async () => {
    setIsLoading(true);
    try {
      const results = await Promise.allSettled(
        selectedRows.map((product) => productApi.delete(product.productId)),
      );
      const deletedCount = results.filter(
        (result) => result.status === "fulfilled",
      ).length;
      if (deletedCount > 0) toast.success(`Đã xóa ${deletedCount} sản phẩm`);
      const failed = results.find((result) => result.status === "rejected");
      if (failed?.status === "rejected") {
        toast.error(
          getApiErrorMessage(
            failed.reason,
            "Không thể xóa một số sản phẩm đã chọn.",
          ),
        );
      }
      resetSelection();
    } catch (error) {
      toast.error(getApiErrorMessage(error, "Không thể xóa sản phẩm đã chọn."));
    } finally {
      setIsLoading(false);
      onChanged();
    }
  };

  return (
    <SplitActionMenu
      primaryLabel="Ngừng kinh doanh"
      primaryIcon={<Lock className="w-4 h-4" />}
      onPrimaryClick={() => handleBulkSetStatus(ProductStatus.Inactive)}
      isLoading={isLoading}
      menuItems={[
        {
          label: "Mở kinh doanh",
          icon: <Unlock className="w-4 h-4" />,
          onClick: () => handleBulkSetStatus(ProductStatus.Active),
        },
        {
          label: "Xóa sản phẩm",
          icon: <Trash2 className="w-4 h-4" />,
          isDestructive: true,
          onClick: () =>
            showConfirmToast({
              title: `Xóa ${selectedRows.length} sản phẩm?`,
              description:
                "Các mục sẽ bị ẩn khỏi danh mục. Dữ liệu hợp đồng đã có vẫn được giữ nguyên.",
              confirmLabel: "Xác nhận xóa",
              onConfirm: handleBulkDelete,
            }),
        },
      ]}
    />
  );
}

const PRODUCT_STATUS_OPTIONS = [
  { label: "Tất cả", value: "All" },
  { label: "Đang kinh doanh", value: String(ProductStatus.Active) },
  { label: "Ngừng kinh doanh", value: String(ProductStatus.Inactive) },
  { label: "Đã xóa", value: String(ProductStatus.Deleted) },
];

export default function ProductListPage() {
  const { can } = usePermission();
  const canManage = can(RBAC_PERMISSIONS.catalogManage);
  const [products, setProducts] = useState<ProductResponse[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [isLoading, setIsLoading] = useState(false);
  const [pagination, setPagination] = useState<PaginationState>({
    pageIndex: 0,
    pageSize: 10,
  });
  const [searchTerm, setSearchTerm] = useState("");
  const [filterStatus, setFilterStatus] = useState<string>("All");
  const [dateRange, setDateRange] = useState<DateRange>({
    from: undefined,
    to: undefined,
  });
  const [loadingId, setLoadingId] = useState<number | null>(null);
  const [toggleStatusId, setToggleStatusId] = useState<number | null>(null);
  const [deleteId, setDeleteId] = useState<number | null>(null);
  const [isToggling, setIsToggling] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);
  const [restoreItem, setRestoreItem] = useState<ProductResponse | null>(null);
  const [isRestoring, setIsRestoring] = useState(false);
  const isDeletedView = filterStatus === String(ProductStatus.Deleted);
  const [isFormOpen, setIsFormOpen] = useState(false);
  const [editingItem, setEditingItem] = useState<ProductResponse | null>(null);
  const [viewingItem, setViewingItem] = useState<ProductResponse | null>(null);
  const listRequest = useRef<AbortController | null>(null);

  const fetchProducts = useCallback(async () => {
    listRequest.current?.abort();
    const controller = new AbortController();
    listRequest.current = controller;
    setIsLoading(true);
    try {
      const statusParam =
        filterStatus !== "All" && filterStatus !== ""
          ? Number(filterStatus)
          : undefined;
      const fromDateParam = dateRange.from
        ? format(dateRange.from, "yyyy-MM-dd")
        : undefined;
      const toDateParam = dateRange.to
        ? format(dateRange.to, "yyyy-MM-dd")
        : undefined;

      const res = await productApi.getList(
        {
          page: pagination.pageIndex + 1,
          pageSize: pagination.pageSize,
          keyword: searchTerm || undefined,
          status: statusParam,
          fromDate: fromDateParam,
          toDate: toDateParam,
        },
        controller.signal,
      );
      if (controller.signal.aborted) return;

      setProducts(res.items || []);
      setTotalCount(res.totalCount || 0);
      if (
        pagination.pageIndex > 0 &&
        pagination.pageIndex >=
          Math.max(1, Math.ceil(res.totalCount / pagination.pageSize))
      ) {
        setPagination((current) => ({
          ...current,
          pageIndex: Math.max(
            0,
            Math.ceil(res.totalCount / current.pageSize) - 1,
          ),
        }));
      }
    } catch (error) {
      if (!controller.signal.aborted)
        toast.error("Lỗi khi tải danh sách sản phẩm");
    } finally {
      if (!controller.signal.aborted) setIsLoading(false);
    }
  }, [
    pagination.pageIndex,
    pagination.pageSize,
    searchTerm,
    filterStatus,
    dateRange.from,
    dateRange.to,
  ]);

  useEffect(() => {
    fetchProducts();
    return () => listRequest.current?.abort();
  }, [fetchProducts]);

  const handleToggleStatusConfirm = async () => {
    if (!toggleStatusId) return;
    setIsToggling(true);
    try {
      const targetProd = products.find((p) => p.productId === toggleStatusId);
      const newStatus =
        targetProd?.status === ProductStatus.Active
          ? ProductStatus.Inactive
          : ProductStatus.Active;

      await productApi.setStatus(toggleStatusId, newStatus);
      toast.success(
        newStatus === ProductStatus.Active
          ? "Đã chuyển sang đang kinh doanh"
          : "Đã ngừng kinh doanh sản phẩm",
      );
      setToggleStatusId(null);
      fetchProducts();
    } catch (error) {
      toast.error("Không thể cập nhật trạng thái");
    } finally {
      setIsToggling(false);
    }
  };

  const handleDeleteConfirm = async () => {
    if (!deleteId) return;
    setIsDeleting(true);
    try {
      await productApi.delete(deleteId);
      toast.success("Đã xóa sản phẩm thành công");
      setDeleteId(null);
      fetchProducts();
    } catch (error) {
      toast.error(getApiErrorMessage(error, "Không thể xóa sản phẩm."));
    } finally {
      setIsDeleting(false);
    }
  };

  const handleRestoreConfirm = async () => {
    if (!restoreItem || isRestoring) return;
    setIsRestoring(true);
    try {
      await productApi.restore(restoreItem.productId);
      toast.success(
        "Đã khôi phục sản phẩm ở trạng thái ngừng kinh doanh. Hãy kiểm tra trước khi mở kinh doanh.",
      );
      setRestoreItem(null);
      await fetchProducts();
    } catch (error) {
      toast.error(getApiErrorMessage(error, "Không thể khôi phục sản phẩm."));
    } finally {
      setIsRestoring(false);
    }
  };

  const columns = useMemo<ColumnDef<ProductResponse>[]>(
    () => [
      {
        id: "select",
        header: ({ table }) => (
          <div className="flex justify-center">
            <Checkbox
              checked={
                table.getIsAllPageRowsSelected() ||
                (table.getIsSomePageRowsSelected() && "indeterminate")
              }
              onCheckedChange={(value) =>
                table.toggleAllPageRowsSelected(!!value)
              }
              aria-label="Select all"
              className="translate-y-[2px]"
            />
          </div>
        ),
        cell: ({ row }) => (
          <div
            className="flex justify-center"
            onClick={(e) => e.stopPropagation()}
          >
            <Checkbox
              checked={row.getIsSelected()}
              onCheckedChange={(value) => row.toggleSelected(!!value)}
              aria-label="Select row"
              className="translate-y-[2px]"
            />
          </div>
        ),
        enableSorting: false,
      },
      {
        accessorKey: "productCode",
        header: "Mã sản phẩm",
        cell: ({ row }) => (
          <span className="font-semibold text-primary/90 block min-w-[90px]">
            {row.original.productCode || "N/A"}
          </span>
        ),
      },
      {
        accessorKey: "productName",
        header: "Tên sản phẩm",
        cell: ({ row }) => (
          <div className="max-w-[280px]">
            <span className="font-medium text-foreground block truncate">
              {row.original.productName}
            </span>
            {row.original.productShortDesc && (
              <span className="text-xs text-muted-foreground block truncate mt-0.5">
                {row.original.productShortDesc}
              </span>
            )}
          </div>
        ),
      },
      {
        accessorKey: "categoryName",
        header: "Danh mục",
        cell: ({ row }) => (
          <div className="flex items-center gap-1.5 min-w-[120px]">
            <Layers className="w-3.5 h-3.5 text-muted-foreground shrink-0" />
            <span className="text-sm">
              {row.original.categoryName || "Chưa phân loại"}
            </span>
          </div>
        ),
      },
      {
        accessorKey: "productPrice",
        header: () => <div className="text-right">Đơn giá</div>,
        cell: ({ row }) => {
          const price = row.original.productPrice;
          return (
            <div className="text-right font-medium min-w-[110px]">
              {price !== undefined && price !== null ? (
                <span className="text-emerald-600 dark:text-emerald-400">
                  {price.toLocaleString("vi-VN")} đ
                </span>
              ) : (
                <span className="text-muted-foreground italic">Liên hệ</span>
              )}
            </div>
          );
        },
      },
      {
        accessorKey: "productCreatedDate",
        header: () => <div className="text-center">Ngày tạo</div>,
        cell: ({ row }) => (
          <div className="text-center text-sm text-muted-foreground min-w-[100px]">
            {row.original.productCreatedDate
              ? format(new Date(row.original.productCreatedDate), "dd/MM/yyyy")
              : "N/A"}
          </div>
        ),
      },
      {
        accessorKey: "status",
        header: () => <div className="text-center">Trạng thái</div>,
        cell: ({ row }) => {
          const statusStr = getProductStatusLabel(row.original.status);
          return (
            <div className="flex justify-center min-w-[130px]">
              <Badge
                variant={
                  row.original.status === ProductStatus.Active
                    ? "outline"
                    : row.original.status === ProductStatus.Inactive
                      ? "warning"
                      : "destructive"
                }
              >
                {statusStr}
              </Badge>
            </div>
          );
        },
      },
      {
        id: "action",
        header: () => <div className="text-right pr-4">Thao tác</div>,
        cell: ({ row }) => {
          const item = row.original;
          const isActive = item.status === ProductStatus.Active;
          const isDeleted = item.status === ProductStatus.Deleted;
          return (
            <SplitActionMenu
              primaryLabel="Chi tiết"
              primaryIcon={<Eye className="w-4 h-4" />}
              onPrimaryClick={() => setViewingItem(item)}
              isLoading={loadingId === item.productId}
              menuItems={
                canManage
                  ? isDeleted
                    ? [
                        {
                          label: "Khôi phục",
                          icon: <RotateCcw className="w-4 h-4" />,
                          onClick: () => setRestoreItem(item),
                        },
                      ]
                    : [
                        {
                          label: "Chỉnh sửa",
                          icon: <UserCog className="w-4 h-4" />,
                          onClick: () => {
                            setEditingItem(item);
                            setIsFormOpen(true);
                          },
                        },
                        {
                          label: isActive
                            ? "Ngừng kinh doanh"
                            : "Mở kinh doanh",
                          icon: isActive ? (
                            <Lock className="w-4 h-4" />
                          ) : (
                            <Unlock className="w-4 h-4" />
                          ),
                          isDestructive: isActive,
                          onClick: () => setToggleStatusId(item.productId),
                        },
                        {
                          label: "Xóa sản phẩm",
                          icon: <Trash2 className="w-4 h-4" />,
                          isDestructive: true,
                          onClick: () => setDeleteId(item.productId),
                        },
                      ]
                  : []
              }
            />
          );
        },
      },
    ],
    [canManage, loadingId],
  );

  const activeCount = useMemo(
    () => products.filter((p) => p.status === ProductStatus.Active).length,
    [products],
  );

  const summaryItems: SummaryCardItem[] = useMemo(
    () => [
      {
        title: "Tổng sản phẩm",
        value: totalCount,
        icon: <Package className="w-6 h-6" />,
        iconWrapperClassName: "bg-primary/10 text-primary",
      },
      {
        title: "Đang kinh doanh (Trang này)",
        value: activeCount,
        icon: <CheckCircle2 className="w-6 h-6" />,
        iconWrapperClassName:
          "bg-emerald-500/10 text-emerald-600 dark:text-emerald-500",
      },
      {
        title: "Ngừng kinh doanh (Trang này)",
        value: products.filter(
          (product) => product.status === ProductStatus.Inactive,
        ).length,
        icon: <Lock className="w-6 h-6" />,
        iconWrapperClassName:
          "bg-amber-500/10 text-amber-600 dark:text-amber-500",
      },
    ],
    [totalCount, activeCount, products],
  );

  const CustomFilters = (
    <>
      <SelectFilter
        value={filterStatus}
        onChange={(val) => {
          setFilterStatus(val);
          setPagination((prev) => ({ ...prev, pageIndex: 0 }));
        }}
        options={PRODUCT_STATUS_OPTIONS}
        placeholder="Trạng thái"
      />
      <DateRangeFilter
        dateRange={dateRange}
        onChange={(range) => {
          setDateRange(range);
          setPagination((prev) => ({ ...prev, pageIndex: 0 }));
        }}
      />
    </>
  );

  const currentToggleItem = products.find(
    (p) => p.productId === toggleStatusId,
  );
  const isTogglingToLock = currentToggleItem?.status === ProductStatus.Active;

  return (
    <>
      <Header />

      <div className="grow overflow-y-auto p-2 lg:p-10 space-y-6">
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div>
            <h1 className="text-2xl font-bold tracking-tight text-foreground">
              Quản lý Sản phẩm
            </h1>
            <p className="text-sm text-muted-foreground mt-1">
              Quản lý danh sách sản phẩm, đơn giá và trạng thái kinh doanh trong
              hệ thống
            </p>
          </div>

          {canManage && (
            <Button
              onClick={() => {
                setEditingItem(null);
                setIsFormOpen(true);
              }}
            >
              <Plus className="w-4 h-4 mr-2" /> Thêm mới
            </Button>
          )}
        </div>

        <SummaryCards items={summaryItems} isLoading={isLoading} />
        {isDeletedView && (
          <p className="text-sm text-muted-foreground">
            Sản phẩm đã xóa chỉ có thể xem hoặc khôi phục. Sau khi khôi phục,
            hãy chọn “Ngừng kinh doanh” để kiểm tra và mở kinh doanh khi cần.
          </p>
        )}

        <Card className="border-border shadow-sm bg-card min-h-[500px] flex flex-col gap-0 p-0">
          <CardContent className="p-4 flex flex-col justify-between flex-1 pb-0">
            <DataTable
              key={isDeletedView ? "deleted" : "current"}
              columns={columns}
              data={products}
              pageCount={Math.ceil(totalCount / pagination.pageSize)}
              pagination={pagination}
              rowCount={totalCount}
              onPaginationChange={setPagination}
              isLoading={isLoading}
              searchPlaceholder="Tìm kiếm tên, mã sản phẩm..."
              filterSlot={CustomFilters}
              onRowClick={(row) => setViewingItem(row)}
              searchValue={searchTerm}
              onSearchChange={(value) => setSearchTerm(value)}
              bulkActions={
                canManage && !isDeletedView
                  ? (selectedRows, resetSelection) => (
                      <ProductBulkActions
                        selectedRows={selectedRows}
                        resetSelection={resetSelection}
                        onChanged={() => void fetchProducts()}
                      />
                    )
                  : undefined
              }
              mobileCardRenderer={(row, { isSelected, actionCell }) => {
                const item = row.original;
                return (
                  <div
                    className={`rounded-xl border bg-card p-4 shadow-sm transition-colors active:bg-secondary/40 ${
                      isSelected
                        ? "border-primary/40 bg-primary/5"
                        : "border-border"
                    }`}
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div>
                        <span className="text-xs font-semibold text-muted-foreground uppercase tracking-wider block">
                          {item.productCode || "CHƯA CÓ MÃ"}
                        </span>
                        <h3 className="text-base font-bold text-foreground mt-0.5">
                          {item.productName}
                        </h3>
                      </div>
                      <StatusBadge
                        className="m-2"
                        status={getProductStatusLabel(item.status)}
                      />
                    </div>

                    <div className="grid grid-cols-2 gap-2 text-sm text-muted-foreground bg-muted/40 p-2.5 rounded-lg border border-border/50 my-2">
                      <div>
                        <span className="text-xs text-muted-foreground block">
                          Danh mục
                        </span>
                        <span className="font-medium text-foreground">
                          {item.categoryName || "Chưa phân loại"}
                        </span>
                      </div>
                      <div>
                        <span className="text-xs text-muted-foreground block">
                          Đơn giá
                        </span>
                        <span className="font-medium text-emerald-600 dark:text-emerald-400">
                          {item.productPrice !== undefined &&
                          item.productPrice !== null
                            ? item.productPrice.toLocaleString("vi-VN") + " đ"
                            : "Liên hệ"}
                        </span>
                      </div>
                    </div>

                    {item.productShortDesc && (
                      <p className="text-xs text-muted-foreground line-clamp-2">
                        {item.productShortDesc}
                      </p>
                    )}

                    {actionCell}
                  </div>
                );
              }}
            />
          </CardContent>
        </Card>

        <ConfirmDialog
          isOpen={!!toggleStatusId}
          onClose={() => setToggleStatusId(null)}
          onConfirm={handleToggleStatusConfirm}
          title={
            isTogglingToLock
              ? "Khóa kinh doanh sản phẩm?"
              : "Mở kinh doanh sản phẩm?"
          }
          description={
            isTogglingToLock
              ? `Bạn có chắc chắn muốn ngừng kinh doanh sản phẩm "${currentToggleItem?.productName}"? Sản phẩm này sẽ không thể tạo báo giá/hợp đồng mới.`
              : `Bạn có chắc chắn muốn mở lại kinh doanh sản phẩm "${currentToggleItem?.productName}"?`
          }
          confirmText={isTogglingToLock ? "Xác nhận ngừng" : "Xác nhận mở"}
          variant={isTogglingToLock ? "destructive" : "default"}
          titleClassName={
            isTogglingToLock ? "text-destructive" : "text-primary"
          }
          isLoading={isToggling}
        />

        <ConfirmDialog
          isOpen={!!deleteId}
          onClose={() => setDeleteId(null)}
          onConfirm={handleDeleteConfirm}
          title="Xóa sản phẩm?"
          description="Sản phẩm sẽ bị ẩn khỏi danh mục và không thể chọn cho hợp đồng mới. Dữ liệu trong hợp đồng, báo giá và đơn hàng đã có vẫn được giữ nguyên. Bạn có thể khôi phục từ danh sách Đã xóa."
          confirmText="Xác nhận xóa"
          variant="destructive"
          titleClassName="text-destructive"
          isLoading={isDeleting}
        />

        <ConfirmDialog
          isOpen={restoreItem !== null}
          onClose={() => !isRestoring && setRestoreItem(null)}
          onConfirm={handleRestoreConfirm}
          isLoading={isRestoring}
          title="Khôi phục sản phẩm?"
          description={`Khôi phục sản phẩm "${restoreItem?.productName}" về trạng thái Ngừng kinh doanh. Dữ liệu cũ được giữ nguyên; hãy kiểm tra thông tin trước khi mở kinh doanh.`}
          confirmText="Khôi phục"
        />

        {canManage && (
          <ProductFormModal
            isOpen={isFormOpen}
            onClose={() => {
              setIsFormOpen(false);
              setEditingItem(null);
            }}
            onSuccess={fetchProducts}
            onViewDeleted={(code) => {
              setIsFormOpen(false);
              setEditingItem(null);
              setFilterStatus(String(ProductStatus.Deleted));
              setSearchTerm(code);
              setDateRange({ from: undefined, to: undefined });
              setPagination((current) => ({ ...current, pageIndex: 0 }));
            }}
            item={editingItem}
          />
        )}

        <ProductFormModal
          isOpen={!!viewingItem}
          onClose={() => setViewingItem(null)}
          onSuccess={() => {}}
          item={viewingItem}
          viewOnly
        />
      </div>
    </>
  );
}
