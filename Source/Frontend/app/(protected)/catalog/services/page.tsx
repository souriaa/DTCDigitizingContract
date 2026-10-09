"use client";

import { useState, useEffect, useMemo, useCallback, useRef } from "react";
import { Header } from "@/components/ui/custom/header";
import {
  Eye,
  UserCog,
  Plus,
  Trash2,
  Package,
  CheckCircle2,
  DollarSign,
  Layers,
  Power,
  RotateCcw,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { toast } from "@/components/ui/sonner";
import { ColumnDef, PaginationState } from "@tanstack/react-table";

import {
  serviceApi,
  ServiceResponse,
  ServiceStatus,
  getServiceStatusLabel,
} from "@/services/catalog/services-api";
import { DataTable } from "@/components/ui/custom/data-table-server";
import { ConfirmDialog } from "@/components/ui/custom/confirm-dialog";
import { getApiErrorMessage } from "@/lib/api-error";
import { showConfirmToast } from "@/components/ui/custom/confirm-toast";
import { SplitActionMenu } from "@/components/ui/custom/split-action-menu";
import { StatusBadge } from "@/components/ui/custom/status-badge";
import {
  SummaryCardItem,
  SummaryCards,
} from "@/components/ui/custom/summary-cards";
import { SelectFilter } from "@/components/ui/custom/select-filter";
import {
  DateRangeFilter,
  DateRange,
} from "@/components/ui/custom/date-range-filter";
import { PageHeaderSkeleton } from "@/components/ui/custom/table-skeleton";
import { format } from "date-fns";
import {
  serviceTypeApi,
  ServiceTypeResponse,
} from "@/services/catalog/service-types-api";
import { ServiceFormModal } from "./service-form-modal";
import { usePermission } from "@/hooks/use-permission";
import { RBAC_PERMISSIONS } from "@/lib/rbac";
import { Badge } from "@/components/ui/badge";

function ServiceBulkActions({
  selectedRows,
  resetSelection,
  onChanged,
}: {
  selectedRows: ServiceResponse[];
  resetSelection: () => void;
  onChanged: () => void;
}) {
  const [isLoading, setIsLoading] = useState(false);

  const handleBulkDelete = async () => {
    setIsLoading(true);
    try {
      const results = await Promise.allSettled(
        selectedRows.map((item) => serviceApi.delete(item.serviceId)),
      );
      const deletedCount = results.filter(
        (result) => result.status === "fulfilled",
      ).length;
      if (deletedCount > 0) toast.success(`Đã xóa ${deletedCount} dịch vụ`);
      const failed = results.find((result) => result.status === "rejected");
      if (failed?.status === "rejected") {
        toast.error(
          getApiErrorMessage(
            failed.reason,
            "Không thể xóa một số dịch vụ đã chọn.",
          ),
        );
      }
      resetSelection();
    } catch (error) {
      toast.error(getApiErrorMessage(error, "Không thể xóa dịch vụ đã chọn."));
    } finally {
      setIsLoading(false);
      onChanged();
    }
  };

  return (
    <SplitActionMenu
      primaryLabel="Xóa dịch vụ"
      primaryIcon={<Trash2 className="w-4 h-4" />}
      onPrimaryClick={() =>
        showConfirmToast({
          title: `Xóa ${selectedRows.length} dịch vụ?`,
          description:
            "Các mục sẽ bị ẩn khỏi danh mục. Dữ liệu hợp đồng đã có vẫn được giữ nguyên.",
          confirmLabel: "Xác nhận xóa",
          onConfirm: handleBulkDelete,
        })
      }
      isLoading={isLoading}
      menuItems={[]}
    />
  );
}

export default function ServiceListPage() {
  const { can } = usePermission();
  const canManage = can(RBAC_PERMISSIONS.catalogManage);
  const [services, setServices] = useState<ServiceResponse[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [isLoading, setIsLoading] = useState(false);
  const [pagination, setPagination] = useState<PaginationState>({
    pageIndex: 0,
    pageSize: 10,
  });
  const [searchTerm, setSearchTerm] = useState("");
  const [combinedFilter, setCombinedFilter] = useState<string>("All");
  const [dateRange, setDateRange] = useState<DateRange>({
    from: undefined,
    to: undefined,
  });
  const [serviceTypes, setServiceTypes] = useState<ServiceTypeResponse[]>([]);
  const [loadingId, setLoadingId] = useState<number | null>(null);
  const [deleteId, setDeleteId] = useState<number | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const [restoreItem, setRestoreItem] = useState<ServiceResponse | null>(null);
  const [isRestoring, setIsRestoring] = useState(false);
  const isDeletedView = combinedFilter === `status_${ServiceStatus.Deleted}`;
  const [isFormOpen, setIsFormOpen] = useState(false);
  const [editingItem, setEditingItem] = useState<ServiceResponse | null>(null);
  const [viewingItem, setViewingItem] = useState<ServiceResponse | null>(null);
  const listRequest = useRef<AbortController | null>(null);

  useEffect(() => {
    serviceTypeApi
      .getList({ pageSize: 100 })
      .then((res) => setServiceTypes(res.items || []))
      .catch(() => {});
  }, []);

  const COMBINED_FILTER_OPTIONS = useMemo(() => {
    const options = [
      { label: "Tất cả", value: "All" },
      { label: "--- TRẠNG THÁI ---", value: "header_status" },
      { label: "Đang hoạt động", value: `status_${ServiceStatus.Active}` },
      { label: "Ngừng hoạt động", value: `status_${ServiceStatus.Inactive}` },
      { label: "Đã xóa", value: `status_${ServiceStatus.Deleted}` },
    ];

    if (serviceTypes.length > 0) {
      options.push({ label: "--- LOẠI DỊCH VỤ ---", value: "header_type" });
      serviceTypes.forEach((st) => {
        options.push({
          label: `${st.serviceTypeName || `Loại #${st.serviceTypeId}`}`,
          value: `type_${st.serviceTypeId}`,
        });
      });
    }

    return options;
  }, [serviceTypes]);

  const fetchServices = useCallback(async () => {
    listRequest.current?.abort();
    const controller = new AbortController();
    listRequest.current = controller;
    setIsLoading(true);
    try {
      let statusParam: number | undefined = undefined;
      let typeIdParam: number | undefined = undefined;

      if (combinedFilter.startsWith("status_")) {
        statusParam = Number(combinedFilter.replace("status_", ""));
      } else if (combinedFilter.startsWith("type_")) {
        typeIdParam = Number(combinedFilter.replace("type_", ""));
      }

      const fromDateParam = dateRange.from
        ? format(dateRange.from, "yyyy-MM-dd")
        : undefined;
      const toDateParam = dateRange.to
        ? format(dateRange.to, "yyyy-MM-dd")
        : undefined;

      const res = await serviceApi.getList(
        {
          page: pagination.pageIndex + 1,
          pageSize: pagination.pageSize,
          keyword: searchTerm || undefined,
          status: statusParam,
          serviceTypeId: typeIdParam,
          fromDate: fromDateParam,
          toDate: toDateParam,
        },
        controller.signal,
      );
      if (controller.signal.aborted) return;

      setServices(res.items || []);
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
        toast.error("Lỗi khi tải danh sách dịch vụ");
    } finally {
      if (!controller.signal.aborted) setIsLoading(false);
    }
  }, [
    pagination.pageIndex,
    pagination.pageSize,
    searchTerm,
    combinedFilter,
    dateRange.from,
    dateRange.to,
  ]);

  useEffect(() => {
    fetchServices();
    return () => listRequest.current?.abort();
  }, [fetchServices]);

  const handleToggleStatus = async (item: ServiceResponse) => {
    setLoadingId(item.serviceId);
    try {
      const newStatus =
        item.status === ServiceStatus.Active
          ? ServiceStatus.Inactive
          : ServiceStatus.Active;
      await serviceApi.setStatus(item.serviceId, newStatus);
      toast.success(
        `Đã chuyển trạng thái sang "${getServiceStatusLabel(newStatus)}"`,
      );
      fetchServices();
    } catch (error: any) {
      const message =
        error?.response?.data?.message || "Không thể cập nhật trạng thái";
      toast.error(message);
    } finally {
      setLoadingId(null);
    }
  };

  const handleDeleteConfirm = async () => {
    if (deleteId === null) return;
    setIsDeleting(true);
    try {
      await serviceApi.delete(deleteId);
      toast.success("Đã xóa dịch vụ thành công");
      setDeleteId(null);
      fetchServices();
    } catch (error: any) {
      const message =
        error?.response?.data?.message || "Không thể xóa dịch vụ này";
      toast.error(message);
    } finally {
      setIsDeleting(false);
    }
  };

  const handleRestoreConfirm = async () => {
    if (!restoreItem || isRestoring) return;
    setIsRestoring(true);
    try {
      await serviceApi.restore(restoreItem.serviceId);
      toast.success(
        "Đã khôi phục dịch vụ ở trạng thái ngừng hoạt động. Hãy kiểm tra trước khi kích hoạt.",
      );
      setRestoreItem(null);
      await fetchServices();
    } catch (error) {
      toast.error(getApiErrorMessage(error, "Không thể khôi phục dịch vụ."));
    } finally {
      setIsRestoring(false);
    }
  };

  const columns = useMemo<ColumnDef<ServiceResponse>[]>(
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
        accessorKey: "serviceName",
        header: "Tên dịch vụ",
        cell: ({ row }) => (
          <div className="max-w-[320px]">
            <span className="font-semibold text-foreground block truncate text-base">
              {row.original.serviceName || "Chưa có tên"}
            </span>
            {row.original.serviceShortDesc && (
              <span className="text-xs text-muted-foreground block truncate mt-0.5">
                {row.original.serviceShortDesc}
              </span>
            )}
          </div>
        ),
      },
      {
        accessorKey: "serviceTypeName",
        header: "Loại dịch vụ",
        cell: ({ row }) => (
          <div className="text-sm font-medium text-foreground min-w-[120px]">
            {row.original.serviceTypeName ? (
              <span className="inline-flex items-center px-2 py-0.5 rounded-md bg-secondary text-secondary-foreground text-xs font-medium">
                <Layers className="w-3 h-3 mr-1" />
                {row.original.serviceTypeName}
              </span>
            ) : (
              <span className="text-muted-foreground italic text-xs">
                Chưa phân loại
              </span>
            )}
          </div>
        ),
      },
      {
        accessorKey: "servicePrice",
        header: () => <div className="text-right">Đơn giá</div>,
        cell: ({ row }) => (
          <div className="text-right font-semibold text-primary min-w-[100px]">
            {row.original.servicePrice !== undefined &&
            row.original.servicePrice !== null
              ? `${row.original.servicePrice.toLocaleString("vi-VN")} VNĐ`
              : "0 VNĐ"}
          </div>
        ),
      },
      {
        accessorKey: "setupPrice",
        header: () => <div className="text-right">Phí khởi tạo</div>,
        cell: ({ row }) => (
          <div className="text-right text-sm text-muted-foreground min-w-[100px]">
            {row.original.setupPrice !== undefined &&
            row.original.setupPrice !== null
              ? `${row.original.setupPrice.toLocaleString("vi-VN")} VNĐ`
              : "-"}
          </div>
        ),
      },
      {
        accessorKey: "status",
        header: () => <div className="text-center">Trạng thái</div>,
        cell: ({ row }) => {
          const isActive = row.original.status === ServiceStatus.Active;
          const statusStr = getServiceStatusLabel(row.original.status);
          return (
            <div className="flex justify-center min-w-[120px]">
              <Badge
                variant={
                  row.original.status === ServiceStatus.Active
                    ? "outline"
                    : row.original.status === ServiceStatus.Inactive
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
          const isActive = item.status === ServiceStatus.Active;
          const isDeleted = item.status === ServiceStatus.Deleted;
          return (
            <SplitActionMenu
              primaryLabel="Chi tiết"
              primaryIcon={<Eye className="w-4 h-4" />}
              onPrimaryClick={() => setViewingItem(item)}
              isLoading={loadingId === item.serviceId}
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
                          label: isActive ? "Ngừng hoạt động" : "Kích hoạt",
                          icon: <Power className="w-4 h-4" />,
                          isDestructive: isActive,
                          onClick: () => handleToggleStatus(item),
                        },
                        {
                          label: "Xóa dịch vụ",
                          icon: <Trash2 className="w-4 h-4" />,
                          isDestructive: true,
                          onClick: () => setDeleteId(item.serviceId),
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
    () =>
      services.filter((item) => item.status === ServiceStatus.Active).length,
    [services],
  );

  const summaryItems: SummaryCardItem[] = useMemo(
    () => [
      {
        title: "Tổng số dịch vụ",
        value: totalCount,
        icon: <Package className="w-6 h-6" />,
        iconWrapperClassName: "bg-primary/10 text-primary",
      },
      {
        title: "Đang hoạt động (Trang này)",
        value: activeCount,
        icon: <CheckCircle2 className="w-6 h-6" />,
        iconWrapperClassName:
          "bg-emerald-500/10 text-emerald-600 dark:text-emerald-500",
      },
    ],
    [totalCount, activeCount],
  );

  const currentDeleteItem = services.find(
    (item) => item.serviceId === deleteId,
  );

  const CustomFilters = (
    <>
      <SelectFilter
        value={combinedFilter}
        onChange={(val) => {
          if (val === "header_status" || val === "header_type") return;
          setCombinedFilter(val);
          setPagination((prev) => ({ ...prev, pageIndex: 0 }));
        }}
        options={COMBINED_FILTER_OPTIONS}
        placeholder="Trạng thái / Loại DV"
        className="w-[210px]"
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

  return (
    <>
      <Header />

      <div className="grow overflow-y-auto p-2 lg:p-10 space-y-6">
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div>
            <h1 className="text-2xl font-bold tracking-tight text-foreground">
              Danh sách Dịch vụ
            </h1>
            <p className="text-sm text-muted-foreground mt-1">
              Quản lý danh sách các gói dịch vụ, đơn giá và chi phí triển khai
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
            Dịch vụ đã xóa chỉ có thể xem hoặc khôi phục. Sau khi khôi phục, hãy
            chọn “Ngừng hoạt động” để kiểm tra và kích hoạt khi cần. Nếu dịch vụ
            cha đã xóa, hãy khôi phục cha trước.
          </p>
        )}

        <Card className="border-border shadow-sm bg-card min-h-[500px] flex flex-col gap-0 p-0">
          <CardContent className="p-4 flex flex-col justify-between flex-1 pb-0">
            <DataTable
              key={isDeletedView ? "deleted" : "current"}
              columns={columns}
              data={services}
              pageCount={Math.ceil(totalCount / pagination.pageSize)}
              pagination={pagination}
              rowCount={totalCount}
              onPaginationChange={setPagination}
              isLoading={isLoading}
              searchPlaceholder="Tìm kiếm tên dịch vụ..."
              filterSlot={CustomFilters}
              onRowClick={(row) => setViewingItem(row)}
              searchValue={searchTerm}
              onSearchChange={(value) => setSearchTerm(value)}
              bulkActions={
                canManage && !isDeletedView
                  ? (selectedRows, resetSelection) => (
                      <ServiceBulkActions
                        selectedRows={selectedRows}
                        resetSelection={resetSelection}
                        onChanged={() => void fetchServices()}
                      />
                    )
                  : undefined
              }
              mobileCardRenderer={(row, { isSelected, actionCell }) => {
                const item = row.original;
                const isActive = item.status === ServiceStatus.Active;
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
                        <span className="text-xs font-semibold text-primary uppercase tracking-wider block">
                          {item.serviceTypeName || "Dịch vụ"}
                        </span>
                        <h3 className="text-base font-bold text-foreground mt-0.5">
                          {item.serviceName || "Chưa có tên"}
                        </h3>
                      </div>
                      <StatusBadge
                        status={getServiceStatusLabel(item.status)}
                      />
                    </div>

                    {item.serviceShortDesc && (
                      <p className="text-xs text-muted-foreground mt-1 line-clamp-2">
                        {item.serviceShortDesc}
                      </p>
                    )}

                    <div className="grid grid-cols-2 gap-2 text-xs bg-muted/40 p-2.5 rounded-lg border border-border/50 my-3">
                      <div>
                        <span className="text-muted-foreground block">
                          Đơn giá
                        </span>
                        <span className="font-bold text-primary text-sm mt-0.5 block">
                          {item.servicePrice !== undefined &&
                          item.servicePrice !== null
                            ? `${item.servicePrice.toLocaleString("vi-VN")} VNĐ`
                            : "0 VNĐ"}
                        </span>
                      </div>
                      <div>
                        <span className="text-muted-foreground block">
                          Phí khởi tạo
                        </span>
                        <span className="font-medium text-foreground mt-0.5 block">
                          {item.setupPrice !== undefined &&
                          item.setupPrice !== null
                            ? `${item.setupPrice.toLocaleString("vi-VN")} VNĐ`
                            : "-"}
                        </span>
                      </div>
                    </div>

                    {actionCell}
                  </div>
                );
              }}
            />
          </CardContent>
        </Card>

        <ConfirmDialog
          isOpen={deleteId !== null}
          onClose={() => setDeleteId(null)}
          onConfirm={handleDeleteConfirm}
          isLoading={isDeleting}
          title="Xóa dịch vụ?"
          description={`Dịch vụ "${currentDeleteItem?.serviceName}" sẽ bị ẩn khỏi danh mục và không thể chọn mới. Dữ liệu hợp đồng đã có vẫn được giữ nguyên. Dịch vụ có dịch vụ con chưa xóa cần được xử lý các dịch vụ con trước.`}
          confirmText="Xác nhận xóa"
          variant="destructive"
          titleClassName="text-destructive"
        />

        <ConfirmDialog
          isOpen={restoreItem !== null}
          onClose={() => !isRestoring && setRestoreItem(null)}
          onConfirm={handleRestoreConfirm}
          isLoading={isRestoring}
          title="Khôi phục dịch vụ?"
          description={`Khôi phục dịch vụ "${restoreItem?.serviceName}" về trạng thái Ngừng hoạt động. Dữ liệu cũ được giữ nguyên; hãy kiểm tra thông tin trước khi kích hoạt.`}
          confirmText="Khôi phục"
        />

        {canManage && (
          <ServiceFormModal
            isOpen={isFormOpen}
            onClose={() => {
              setIsFormOpen(false);
              setEditingItem(null);
            }}
            onSuccess={fetchServices}
            item={editingItem}
          />
        )}

        <ServiceFormModal
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
