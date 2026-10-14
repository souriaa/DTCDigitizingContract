"use client";
import { useEffect, useMemo, useRef, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { format } from "date-fns";
import {
  FolderTree,
  Plus,
  MoreHorizontal,
  Pencil,
  Trash2,
  Eye,
  BriefcaseBusiness,
} from "lucide-react";
import { Header } from "@/components/ui/custom/header";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Tabs, TabsList, TabsTrigger, TabsContent } from "@/components/ui/tabs";
import {
  Card,
  CardHeader,
  CardTitle,
  CardDescription,
  CardContent,
} from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Alert, AlertTitle, AlertDescription } from "@/components/ui/alert";
import { Empty, EmptyDescription } from "@/components/ui/empty";
import { Spinner } from "@/components/ui/spinner";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { SelectFilter } from "@/components/ui/custom/select-filter";
import { ConfirmDialog } from "@/components/ui/custom/confirm-dialog";
import { DateFilter } from "@/components/ui/custom/date-filter";
import { PermissionGuard } from "@/components/auth/permission-guard";
import { toast } from "@/components/ui/sonner";
import { getApiErrorMessage } from "@/lib/api-error";
import { RBAC_PERMISSIONS } from "@/lib/rbac";
import { usePermission } from "@/hooks/use-permission";
import { useAuthStore } from "@/hooks/use-auth-store";
import { getStoredTablePageSize } from "@/services/preferences-api";
import {
  productApi,
  type ProductResponse,
} from "@/services/catalog/products-api";
import {
  serviceApi,
  type ServiceResponse,
} from "@/services/catalog/services-api";
import {
  categoryApi,
  type CategoryResponse,
} from "@/services/catalog/category-api";
import {
  serviceTypeApi,
  type ServiceTypeResponse,
} from "@/services/catalog/service-types-api";
import {
  workspaceApi,
  type CatalogKind,
  type CatalogFilter,
  type CatalogGroups,
  type CatalogGroup,
  type CatalogItem,
  type CatalogPage,
} from "@/services/catalog/workspace-api";
import {
  ancestorKeys,
  catalogKey,
  catalogRootGroups,
  catalogTreePageForGroup,
  createCatalogQueue,
} from "./catalog-state";
import { CatalogGroupTree } from "./catalog-group-tree";
import { CatalogGroupPanel } from "./catalog-group-panel";
import {
  CatalogItemList,
  type CatalogPageState,
  type ItemAction,
} from "./catalog-item-list";
import { ProductFormModal } from "./product-form-modal";
import { ServiceFormModal } from "./service-form-modal";
import { CategoryFormModal } from "./category-form-modal";
import { ServiceTypeFormModal } from "./service-type-form-modal";

type Modal =
  | {
      type: "product";
      item?: ProductResponse;
      group?: number | null;
      view?: boolean;
    }
  | {
      type: "service";
      item?: ServiceResponse;
      group?: number | null;
      view?: boolean;
    }
  | {
      type: "category";
      item?: CategoryResponse;
      parent?: number | null;
      view?: boolean;
    }
  | { type: "service-type"; item?: ServiceTypeResponse; view?: boolean };
type Confirm = {
  title: string;
  description: string;
  destructive?: boolean;
  run: () => Promise<void>;
};
const emptyFilters: CatalogFilter = {};
const parseDate = (value?: string) =>
  value ? new Date(`${value}T00:00:00`) : undefined;
const dateValue = (value: Date | undefined) =>
  value ? format(value, "yyyy-MM-dd") : undefined;

function urlFilters(params: {
  get: (key: string) => string | null;
}): CatalogFilter {
  const date = (key: string) => {
    const value = params.get(key);
    if (
      !value ||
      !/^\d{4}-\d{2}-\d{2}$/.test(value) ||
      value.startsWith("0000")
    )
      return undefined;
    const parsed = parseDate(value)!;
    return Number.isFinite(parsed.getTime()) && dateValue(parsed) === value
      ? value
      : undefined;
  };
  return {
    keyword: params.get("keyword") || undefined,
    status: ["0", "1", "2"].includes(params.get("status") ?? "")
      ? Number(params.get("status"))
      : undefined,
    fromDate: date("fromDate"),
    toDate: date("toDate"),
  };
}

export function CatalogWorkspace() {
  const user = useAuthStore((state) => state.user);
  const { can } = usePermission();
  if (!can(RBAC_PERMISSIONS.catalogRead))
    return (
      <PermissionGuard permission={RBAC_PERMISSIONS.catalogRead}>
        {null}
      </PermissionGuard>
    );
  return (
    <WorkspaceContent
      key={`${user?.tenantId}:${user?.employeeId}`}
      scope={`${user?.tenantId}:${user?.employeeId}`}
      canManage={can(RBAC_PERMISSIONS.catalogManage)}
    />
  );
}

function WorkspaceContent({
  scope,
  canManage,
}: {
  scope: string;
  canManage: boolean;
}) {
  const router = useRouter();
  const params = useSearchParams();
  const kind: CatalogKind =
    params.get("tab") === "services" ? "services" : "products";
  const [filters, setFilters] = useState<Record<CatalogKind, CatalogFilter>>(
    () => {
      const initial = urlFilters(params);
      return {
        products: kind === "products" ? initial : {},
        services: kind === "services" ? initial : {},
      };
    },
  );
  const [expanded, setExpanded] = useState<Set<string>>(() => new Set());
  const [pages, setPages] = useState<Record<string, CatalogPageState>>({});
  const [treePages, setTreePages] = useState<
    Record<CatalogKind, CatalogPageState>
  >({
    products: { page: 1, pageSize: 5 },
    services: { page: 1, pageSize: 5 },
  });
  const pendingTreeGroup = useRef<
    Partial<Record<CatalogKind, { id: number; revision: number }>>
  >({});
  const [revisions, setRevisions] = useState<Record<string, number>>({});
  const [metaRevision, setMetaRevision] = useState({
    products: 0,
    services: 0,
  });
  const [metadata, setMetadata] = useState<
    Partial<Record<CatalogKind, { key: string; value: CatalogGroups }>>
  >({});
  const [metaError, setMetaError] = useState("");
  const [metaLoading, setMetaLoading] = useState(false);
  const [modal, setModal] = useState<Modal | null>(null);
  const [confirm, setConfirm] = useState<Confirm | null>(null);
  const [confirmBusy, setConfirmBusy] = useState(false);
  const [detailBusy, setDetailBusy] = useState(false);
  const detailSequence = useRef(0);
  const writtenUrls = useRef(new Set<string>());
  const handledGroup = useRef("");
  const [scrollTarget, setScrollTarget] = useState<string | null>(null);
  const urlKey = params.toString();
  useEffect(
    () => () => {
      detailSequence.current++;
    },
    [],
  );
  useEffect(() => {
    if (writtenUrls.current.delete(urlKey)) return;
    const query = new URLSearchParams(urlKey);
    const targetKind =
      query.get("tab") === "services" ? "services" : "products";
    const next = urlFilters(query);
    const timer = setTimeout(
      () =>
        setFilters((current) =>
          JSON.stringify(current[targetKind]) === JSON.stringify(next)
            ? current
            : { ...current, [targetKind]: next },
        ),
      0,
    );
    return () => clearTimeout(timer);
  }, [urlKey]);
  const [cache] = useState(
    () => new Map<string, { revision: number; data: CatalogPage }>(),
  );
  const [queue] = useState(() => createCatalogQueue());
  const [defaultSize] = useState(() => getStoredTablePageSize() ?? 20);
  const [debouncedSearch, setDebouncedSearch] = useState({
    kind,
    value: filters[kind].keyword ?? "",
  });
  const filter = filters[kind];
  const debouncedKeyword =
    debouncedSearch.kind === kind
      ? debouncedSearch.value
      : (filter.keyword?.trim() ?? "");
  useEffect(() => {
    const timer = setTimeout(
      () => setDebouncedSearch({ kind, value: filter.keyword?.trim() ?? "" }),
      250,
    );
    return () => clearTimeout(timer);
  }, [filter.keyword, kind]);
  const effectiveFilter = { ...filter, keyword: debouncedKeyword || undefined };
  const groupQueryKey = JSON.stringify([
    kind,
    effectiveFilter.status,
    effectiveFilter.fromDate,
    effectiveFilter.toDate,
    effectiveFilter.keyword,
  ]);
  const metaKey = `${groupQueryKey}:${metaRevision[kind]}`;
  useEffect(() => {
    const controller = new AbortController();
    const load = async () => {
      setMetaLoading(true);
      setMetaError("");
      try {
        const data = await queue.run(
          () => workspaceApi.groups(kind, effectiveFilter, controller.signal),
          controller.signal,
        );
        if (!controller.signal.aborted) {
          setMetadata((current) => ({
            ...current,
            [kind]: { key: groupQueryKey, value: data },
          }));
          const pendingGroup = pendingTreeGroup.current[kind];
          const revealId =
            pendingGroup &&
            metaRevision[kind] >= pendingGroup.revision &&
            data.groups.some((node) => node.id === pendingGroup.id)
              ? pendingGroup.id
              : undefined;
          if (revealId != null) delete pendingTreeGroup.current[kind];
          setTreePages((current) => {
            const pagination = current[kind];
            const lastPage = Math.max(
              1,
              Math.ceil(
                catalogRootGroups(data.groups, kind).length /
                  pagination.pageSize,
              ),
            );
            const requestedPage =
              revealId == null
                ? undefined
                : catalogTreePageForGroup(
                    data.groups,
                    kind,
                    revealId,
                    pagination.pageSize,
                  );
            return {
              ...current,
              [kind]: {
                ...pagination,
                page: requestedPage ?? Math.min(pagination.page, lastPage),
              },
            };
          });
          if (
            revealId != null &&
            data.groups.some((node) => node.id === revealId)
          ) {
            setExpanded(
              (current) =>
                new Set([
                  ...current,
                  ...ancestorKeys(data.groups, revealId, kind),
                ]),
            );
          }
        }
      } catch (error) {
        if (!controller.signal.aborted)
          setMetaError(getApiErrorMessage(error, "Không thể tải danh mục."));
      } finally {
        if (!controller.signal.aborted) setMetaLoading(false);
      }
    };
    void load();
    return () => controller.abort();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [metaKey, queue]);
  const groups =
    metadata[kind]?.key === groupQueryKey ? metadata[kind]!.value : null;
  const searching = !!debouncedKeyword;
  const requestedGroup = params.get("groupId");
  useEffect(() => {
    if (!requestedGroup || !groups) return;
    const signature = `${kind}:${requestedGroup}`;
    if (handledGroup.current === signature) return;
    const id = requestedGroup === "unassigned" ? null : Number(requestedGroup);
    if (id != null && !groups.groups.some((node) => node.id === id)) return;
    const timer = setTimeout(() => {
      handledGroup.current = signature;
      setExpanded(
        (current) =>
          new Set([
            ...current,
            ...(id == null
              ? [catalogKey(kind, null)]
              : ancestorKeys(groups.groups, id, kind)),
          ]),
      );
      setScrollTarget(`catalog-${kind}-${id ?? "unassigned"}`);
      if (id != null)
        setTreePages((current) => {
          const pagination = current[kind];
          const page = catalogTreePageForGroup(
            groups.groups,
            kind,
            id,
            pagination.pageSize,
          );
          return page == null
            ? current
            : { ...current, [kind]: { ...pagination, page } };
        });
    }, 0);
    return () => clearTimeout(timer);
  }, [groups, requestedGroup, kind]);
  useEffect(() => {
    if (!scrollTarget || searching || !groups) return;
    const frame = requestAnimationFrame(() => {
      const element = document.getElementById(scrollTarget);
      if (!element) return;
      element.scrollIntoView({ block: "start", behavior: "smooth" });
      element.focus({ preventScroll: true });
      setScrollTarget(null);
    });
    return () => cancelAnimationFrame(frame);
  }, [
    scrollTarget,
    searching,
    groups,
    treePages[kind].page,
    treePages[kind].pageSize,
  ]);
  const updateUrl = (nextKind: CatalogKind, nextFilter: CatalogFilter) => {
    const query = new URLSearchParams({ tab: nextKind });
    for (const [key, value] of Object.entries(nextFilter))
      if (value != null && value !== "") query.set(key, String(value));
    if (writtenUrls.current.size > 50) writtenUrls.current.clear();
    writtenUrls.current.add(query.toString());
    router.replace(`/catalog/products?${query}`, { scroll: false });
  };
  const changeFilter = (patch: Partial<CatalogFilter>) => {
    const next = { ...filters[kind], ...patch };
    setFilters((current) => ({ ...current, [kind]: next }));
    const onlySearch = Object.keys(patch).every((key) => key === "keyword");
    setPages((current) =>
      Object.fromEntries(
        Object.entries(current).map(([key, page]) => [
          key,
          (
            onlySearch
              ? key === catalogKey(kind, "search")
              : key.startsWith(`${kind}:`)
          )
            ? { ...page, page: 1 }
            : page,
        ]),
      ),
    );
    updateUrl(kind, next);
  };
  const toggle = (key: string) =>
    setExpanded((current) => {
      const next = new Set(current);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  const openGroup = (
    id: number | null,
    targetKind: CatalogKind = kind,
    leaveSearch = true,
  ) => {
    const nodes = metadata[targetKind]?.value.groups ?? [];
    if (id != null)
      setTreePages((current) => {
        const pagination = current[targetKind];
        const page = catalogTreePageForGroup(
          nodes,
          targetKind,
          id,
          pagination.pageSize,
        );
        return page == null
          ? current
          : { ...current, [targetKind]: { ...pagination, page } };
      });
    setExpanded(
      (current) =>
        new Set([
          ...current,
          ...(id == null
            ? [catalogKey(targetKind, null)]
            : ancestorKeys(nodes, id, targetKind)),
        ]),
    );
    if (leaveSearch && targetKind === kind && filters[kind].keyword)
      changeFilter({ keyword: undefined });
    if (leaveSearch)
      setScrollTarget(`catalog-${targetKind}-${id ?? "unassigned"}`);
  };
  const invalidate = (targetKind: CatalogKind, ids: Array<number | null>) => {
    setRevisions((current) => {
      const next = { ...current };
      for (const id of new Set(ids)) {
        const key = catalogKey(targetKind, id);
        next[key] = (next[key] ?? 0) + 1;
      }
      const searchKey = catalogKey(targetKind, "search");
      next[searchKey] = (next[searchKey] ?? 0) + 1;
      return next;
    });
    setMetaRevision((current) => ({
      ...current,
      [targetKind]: current[targetKind] + 1,
    }));
  };
  const action = async (
    targetKind: CatalogKind,
    item: CatalogItem,
    operation: ItemAction,
  ) => {
    if (operation !== "view" && !canManage) return;
    if (operation === "view" || operation === "edit") {
      const request = ++detailSequence.current;
      setDetailBusy(true);
      try {
        const next: Modal =
          targetKind === "products"
            ? {
                type: "product",
                item: await workspaceApi.product(item.id),
                view: operation === "view" || item.status === 2,
              }
            : {
                type: "service",
                item: await workspaceApi.service(item.id),
                view: operation === "view" || item.status === 2,
              };
        if (request === detailSequence.current) setModal(next);
      } catch (error) {
        if (request === detailSequence.current)
          toast.error(getApiErrorMessage(error, "Không thể tải chi tiết."));
      } finally {
        if (request === detailSequence.current) setDetailBusy(false);
      }
      return;
    }
    const title =
      operation === "delete"
        ? "Xóa mặt hàng"
        : operation === "restore"
          ? "Khôi phục mặt hàng"
          : item.status === 1
            ? "Ngừng hoạt động"
            : "Mở hoạt động";
    setConfirm({
      title,
      destructive: operation === "delete",
      description: `${title} “${item.name}”? ${operation === "restore" ? "Mặt hàng được khôi phục ở trạng thái ngừng hoạt động." : operation === "delete" ? "Dữ liệu trong hợp đồng và báo giá được giữ nguyên." : ""}`,
      run: async () => {
        const api = targetKind === "products" ? productApi : serviceApi;
        if (operation === "delete") await api.delete(item.id);
        else if (operation === "restore") await api.restore(item.id);
        else await api.setStatus(item.id, item.status === 1 ? 0 : 1);
        invalidate(targetKind, [item.groupId]);
        toast.success("Đã cập nhật mặt hàng.");
      },
    });
  };
  const bulkDelete = (targetKind: CatalogKind, items: CatalogItem[]) => {
    if (!canManage || !items.length) return;
    setConfirm({
      title: "Xóa các mục đã chọn",
      description: `Xóa ${items.length} mặt hàng đã chọn trên trang hiện tại? Không xóa các dòng chưa tải.`,
      destructive: true,
      run: async () => {
        const results = await Promise.allSettled(
          items.map((item) =>
            (targetKind === "products" ? productApi : serviceApi).delete(
              item.id,
            ),
          ),
        );
        const success = results.filter(
          (result) => result.status === "fulfilled",
        ).length;
        invalidate(
          targetKind,
          items.map((item) => item.groupId),
        );
        if (success) toast.success(`Đã xóa ${success}/${items.length} mục.`);
        if (success !== items.length)
          toast.error(
            "Một số mục không thể xóa. Vui lòng kiểm tra lại dữ liệu và quan hệ dịch vụ cha/con.",
          );
      },
    });
  };
  const groupAction = async (
    node: CatalogGroup,
    operation: "view" | "edit" | "delete",
  ) => {
    if (operation !== "view" && !canManage) return;
    if (operation === "delete") {
      setConfirm({
        title: kind === "products" ? "Xóa danh mục" : "Xóa loại dịch vụ",
        destructive: true,
        description: `Xóa “${node.name}”${kind === "products" ? ` và ${node.descendantCount} danh mục con` : ""}? Không thể xóa nếu còn mặt hàng tham chiếu, kể cả mặt hàng đã xóa mềm.`,
        run: async () => {
          if (kind === "products") await categoryApi.delete(node.id);
          else await serviceTypeApi.delete(node.id);
          invalidate(kind, [node.id]);
          toast.success("Đã xóa danh mục.");
        },
      });
      return;
    }
    const request = ++detailSequence.current;
    setDetailBusy(true);
    try {
      const next: Modal =
        kind === "products"
          ? {
              type: "category",
              item: await categoryApi.getById(node.id),
              view: operation === "view",
            }
          : {
              type: "service-type",
              item: await serviceTypeApi.getById(node.id),
              view: operation === "view",
            };
      if (request === detailSequence.current) setModal(next);
    } catch (error) {
      if (request === detailSequence.current)
        toast.error(
          getApiErrorMessage(error, "Không thể tải chi tiết danh mục."),
        );
    } finally {
      if (request === detailSequence.current) setDetailBusy(false);
    }
  };
  const groupActions = (node: CatalogGroup) => (
    <>
      {canManage && (
        <Button
          variant="ghost"
          size="icon"
          aria-label={`Thêm ${kind === "products" ? "sản phẩm" : "dịch vụ"} trong ${node.name}`}
          onClick={() =>
            showModal(
              kind === "products"
                ? { type: "product", group: node.id }
                : { type: "service", group: node.id },
            )
          }
        >
          <Plus className="size-4" />
        </Button>
      )}
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button
            variant="ghost"
            size="icon"
            aria-label={`Thao tác danh mục ${node.name}`}
          >
            <MoreHorizontal className="size-4" />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end">
          <DropdownMenuItem onSelect={() => void groupAction(node, "view")}>
            <Eye />
            Xem danh mục
          </DropdownMenuItem>
          {canManage && (
            <>
              <DropdownMenuItem onSelect={() => void groupAction(node, "edit")}>
                <Pencil />
                Chỉnh sửa danh mục
              </DropdownMenuItem>
              {kind === "products" && (
                <DropdownMenuItem
                  onSelect={() =>
                    showModal({ type: "category", parent: node.id })
                  }
                >
                  <FolderTree />
                  Thêm danh mục con
                </DropdownMenuItem>
              )}
              <DropdownMenuItem
                className="text-destructive"
                onSelect={() => void groupAction(node, "delete")}
              >
                <Trash2 />
                Xóa danh mục
              </DropdownMenuItem>
            </>
          )}
        </DropdownMenuContent>
      </DropdownMenu>
    </>
  );
  const renderList = (
    targetKind: CatalogKind,
    id: number | null | "search",
    active: boolean,
  ) => {
    const key = catalogKey(targetKind, id);
    const pagination = pages[key] ?? { page: 1, pageSize: defaultSize };
    const targetFilter =
      targetKind === kind ? effectiveFilter : filters[targetKind];
    return (
      <CatalogItemList
        kind={targetKind}
        active={active}
        scope={scope}
        filter={{
          ...targetFilter,
          ...pagination,
          ...(id === "search"
            ? {}
            : id == null
              ? { unassignedOnly: true }
              : { groupId: id }),
        }}
        pagination={pagination}
        onPage={(value) =>
          setPages((current) => ({ ...current, [key]: value }))
        }
        revision={revisions[key] ?? 0}
        cache={cache}
        queue={queue}
        canManage={canManage}
        onAction={(item, operation) => void action(targetKind, item, operation)}
        onBulkDelete={(items) => bulkDelete(targetKind, items)}
        onOpenGroup={
          id === "search" ? (group) => openGroup(group, targetKind) : undefined
        }
      />
    );
  };
  const showModal = (next: Modal | null) => {
    detailSequence.current++;
    setDetailBusy(false);
    setModal(next);
  };
  const closeModal = () => showModal(null);
  const matchingGroups = useMemo(
    () =>
      groups?.groups.filter((node) =>
        node.path
          .toLocaleLowerCase("vi")
          .includes(debouncedKeyword.toLocaleLowerCase("vi")),
      ) ?? [],
    [groups, debouncedKeyword],
  );
  const runConfirm = async () => {
    if (!confirm || confirmBusy) return;
    setConfirmBusy(true);
    try {
      await confirm.run();
      setConfirm(null);
    } catch (error) {
      toast.error(getApiErrorMessage(error, "Không thể thực hiện thao tác."));
    } finally {
      setConfirmBusy(false);
    }
  };
  return (
    <>
      <Header title="Sản phẩm và dịch vụ" />
      <main className="grow overflow-y-auto p-3 lg:p-8">
        <Tabs
          value={kind}
          onValueChange={(value) => {
            const nextKind = value as CatalogKind;
            updateUrl(nextKind, filters[nextKind]);
          }}
          className="mx-auto space-y-5"
        >
          <div>
            <h1 className="text-2xl font-bold">Sản phẩm và dịch vụ</h1>
            <p className="text-sm text-muted-foreground">
              Mở danh mục để quản lý mặt hàng trực tiếp. Danh mục con được hiển
              thị riêng.
            </p>
          </div>
          <TabsList aria-label="Loại mặt hàng">
            <TabsTrigger value="products">Sản phẩm</TabsTrigger>
            <TabsTrigger value="services">Dịch vụ</TabsTrigger>
          </TabsList>
          <Card className="min-w-0 py-4 sm:py-6">
            <CardHeader className="border-b flex justify-between">
              <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-start">
                <div>
                  <CardTitle className="flex items-center gap-2">
                    {kind === "products" ? (
                      <FolderTree className="size-5 text-primary" />
                    ) : (
                      <BriefcaseBusiness className="size-5 text-primary" />
                    )}
                    {kind === "products"
                      ? "Quản lý sản phẩm"
                      : "Quản lý dịch vụ"}
                  </CardTitle>
                  <CardDescription className="mt-1.5">
                    {kind === "products"
                      ? "Mở danh mục để xem sản phẩm trực tiếp hoặc các danh mục con."
                      : "Mở loại dịch vụ để xem và quản lý các dịch vụ thuộc nhóm."}
                  </CardDescription>
                </div>
              </div>
              <div
                className="flex shrink-0 flex-wrap items-center gap-2 sm:justify-end"
                aria-live="polite"
                aria-busy={metaLoading}
              >
                {metaLoading ? (
                  <>
                    <Skeleton className="h-7 w-24 rounded-full" />
                    <Skeleton className="h-7 w-36 rounded-full" />
                  </>
                ) : groups && !metaError ? (
                  <>
                    <Badge
                      variant="outline"
                      className="rounded-full px-3 py-1 font-normal"
                    >
                      <span className="font-semibold">
                        {groups.groups.length}
                      </span>
                      {kind === "products" ? "danh mục" : "loại dịch vụ"}
                    </Badge>
                    <Badge
                      variant="secondary"
                      className="rounded-full px-3 py-1 font-normal"
                    >
                      <span className="font-semibold">{groups.totalCount}</span>
                      {kind === "products"
                        ? "sản phẩm phù hợp"
                        : "dịch vụ phù hợp"}
                    </Badge>
                  </>
                ) : null}
              </div>
            </CardHeader>
            <CardContent className="space-y-5 px-3 sm:px-6">
              <div className="flex flex-wrap items-center gap-2">
                <Input
                  className="w-full"
                  aria-label="Tìm kiếm toàn bộ danh mục và mặt hàng"
                  placeholder="Tìm danh mục, tên hoặc mã mặt hàng..."
                  value={filter.keyword ?? ""}
                  onChange={(event) =>
                    changeFilter({ keyword: event.target.value || undefined })
                  }
                />
                <SelectFilter
                  value={
                    filter.status !== undefined ? String(filter.status) : "all"
                  }
                  onChange={(value) =>
                    changeFilter({
                      status: value === "all" ? undefined : Number(value),
                    })
                  }
                  ariaLabel="Trạng thái mặt hàng"
                  placeholder="Trạng thái mặt hàng"
                  options={[
                    { value: "all", label: "Hiện hành" },
                    { value: "1", label: "Hoạt động" },
                    { value: "0", label: "Ngừng hoạt động" },
                    { value: "2", label: "Đã xóa" },
                  ]}
                />
                <DateFilter
                  date={parseDate(filter.fromDate)}
                  placeholder="Mặt hàng từ ngày"
                  limit={{ to: parseDate(filter.toDate) }}
                  onChange={(value) =>
                    changeFilter({ fromDate: dateValue(value) })
                  }
                />
                <DateFilter
                  date={parseDate(filter.toDate)}
                  placeholder="Mặt hàng đến ngày"
                  limit={{ from: parseDate(filter.fromDate) }}
                  onChange={(value) =>
                    changeFilter({ toDate: dateValue(value) })
                  }
                />
                <Button
                  variant="ghost"
                  onClick={() => {
                    setFilters((current) => ({
                      ...current,
                      [kind]: emptyFilters,
                    }));
                    setPages((current) =>
                      Object.fromEntries(
                        Object.entries(current).map(([key, page]) => [
                          key,
                          key.startsWith(`${kind}:`)
                            ? { ...page, page: 1 }
                            : page,
                        ]),
                      ),
                    );
                    updateUrl(kind, {});
                  }}
                >
                  Xóa bộ lọc
                </Button>
              </div>
              <div className="flex flex-wrap items-center gap-2">
                {canManage && (
                  <>
                    <Button
                      variant="outline"
                      onClick={() =>
                        showModal(
                          kind === "products"
                            ? { type: "category" }
                            : { type: "service-type" },
                        )
                      }
                    >
                      <Plus className="size-4" />
                      Thêm {kind === "products" ? "danh mục" : "loại dịch vụ"}
                    </Button>
                    <Button
                      onClick={() =>
                        showModal(
                          kind === "products"
                            ? { type: "product" }
                            : { type: "service" },
                        )
                      }
                    >
                      <Plus className="size-4" />
                      Thêm {kind === "products" ? "sản phẩm" : "dịch vụ"}
                    </Button>
                  </>
                )}
                <Button
                  variant="ghost"
                  onClick={() =>
                    setExpanded(
                      (current) =>
                        new Set(
                          [...current].filter(
                            (key) => !key.startsWith(`${kind}:`),
                          ),
                        ),
                    )
                  }
                >
                  Thu gọn tất cả
                </Button>
              </div>
              {detailBusy && (
                <Spinner
                  text="Đang tải chi tiết..."
                  className="flex-row justify-start gap-2"
                  iconClassName="size-4"
                />
              )}
              {filter.status === 2 && (
                <Alert>
                  <AlertDescription>
                    Mặt hàng đã xóa chỉ có thể xem hoặc khôi phục. Khôi phục
                    không tự mở trạng thái hoạt động.
                  </AlertDescription>
                </Alert>
              )}
              {metaError ? (
                <Alert variant="destructive">
                  <AlertTitle>Không thể tải danh mục</AlertTitle>
                  <AlertDescription>
                    <p>{metaError}</p>
                    <Button
                      variant="outline"
                      onClick={() =>
                        setMetaRevision((current) => ({
                          ...current,
                          [kind]: current[kind] + 1,
                        }))
                      }
                    >
                      Thử lại
                    </Button>
                  </AlertDescription>
                </Alert>
              ) : (
                <>
                  {metaLoading && (
                    <Spinner
                      text="Đang tải cây danh mục..."
                      className="flex-row justify-start gap-2"
                      iconClassName="size-4"
                    />
                  )}
                  {searching ? (
                    <TabsContent value={kind} className="space-y-4">
                      <Card className="gap-3 py-3">
                        <CardHeader className="px-3">
                          <CardTitle role="heading" aria-level={2}>
                            Danh mục phù hợp
                          </CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-2 px-3">
                          {matchingGroups.length === 0 && !metaLoading && (
                            <Empty className="py-3 md:py-3">
                              <EmptyDescription>
                                Không có danh mục phù hợp.
                              </EmptyDescription>
                            </Empty>
                          )}
                          {matchingGroups.map((node) => (
                            <Button
                              key={node.id}
                              variant="outline"
                              className="h-auto max-w-full whitespace-normal text-left"
                              onClick={() => openGroup(node.id)}
                            >
                              {node.path}
                            </Button>
                          ))}
                        </CardContent>
                      </Card>
                      <Card className="gap-3 py-3">
                        <CardHeader className="px-3">
                          <CardTitle role="heading" aria-level={2}>
                            Mặt hàng phù hợp trong toàn bộ danh mục
                          </CardTitle>
                        </CardHeader>
                        <CardContent className="px-3">
                          {renderList(kind, "search", !metaLoading)}
                        </CardContent>
                      </Card>
                    </TabsContent>
                  ) : (
                    (["products", "services"] as const).map((tab) => {
                      const data = tab === kind ? groups : metadata[tab]?.value;
                      return (
                        <TabsContent
                          key={tab}
                          value={tab}
                          forceMount
                          hidden={tab !== kind}
                          className="space-y-3 data-[state=inactive]:hidden"
                        >
                          {data && (
                            <>
                              <CatalogGroupTree
                                kind={tab}
                                nodes={data.groups}
                                expanded={expanded}
                                active={tab === kind}
                                pagination={treePages[tab]}
                                onPaginationChange={(value) =>
                                  setTreePages((current) => ({
                                    ...current,
                                    [tab]: value,
                                  }))
                                }
                                onToggle={toggle}
                                actions={
                                  tab === kind ? groupActions : () => null
                                }
                                list={(node, enabled) =>
                                  renderList(tab, node.id, enabled)
                                }
                              />
                              <div
                                id={`catalog-${tab}-unassigned`}
                                tabIndex={-1}
                              >
                                <CatalogGroupPanel
                                  name={
                                    tab === "products"
                                      ? "Chưa phân danh mục"
                                      : "Chưa phân loại"
                                  }
                                  path="Chỉ gồm mặt hàng chưa gắn nhóm; cần chọn nhóm khi chỉnh sửa"
                                  count={data.unassignedCount}
                                  subtreeCount={data.unassignedCount}
                                  open={expanded.has(catalogKey(tab, null))}
                                  onToggle={() => toggle(catalogKey(tab, null))}
                                >
                                  {renderList(
                                    tab,
                                    null,
                                    tab === kind &&
                                      expanded.has(catalogKey(tab, null)),
                                  )}
                                </CatalogGroupPanel>
                              </div>
                              {data.groups.length === 0 && (
                                <Empty className="py-3 md:py-3">
                                  <EmptyDescription>
                                    Chưa có danh mục. Hãy thêm danh mục trước
                                    khi tạo mặt hàng.
                                  </EmptyDescription>
                                </Empty>
                              )}
                            </>
                          )}
                        </TabsContent>
                      );
                    })
                  )}
                </>
              )}
            </CardContent>
          </Card>
        </Tabs>
      </main>
      {modal?.type === "product" && (
        <ProductFormModal
          isOpen
          onClose={closeModal}
          item={modal.item}
          viewOnly={modal.view}
          initialCategoryId={modal.group}
          onSuccess={(saved) => {
            invalidate("products", [
              modal.item?.categoryId ?? null,
              saved?.categoryId ?? null,
            ]);
            if (saved?.categoryId != null)
              openGroup(saved.categoryId, "products", false);
          }}
          onViewDeleted={(code) => {
            closeModal();
            changeFilter({ status: 2, keyword: code });
          }}
        />
      )}
      {modal?.type === "service" && (
        <ServiceFormModal
          isOpen
          onClose={closeModal}
          item={modal.item}
          viewOnly={modal.view}
          initialServiceTypeId={modal.group}
          onSuccess={(saved) => {
            invalidate(
              "services",
              modal.item?.serviceName !== saved?.serviceName
                ? [
                    ...(metadata.services?.value.groups.map(
                      (node) => node.id,
                    ) ?? []),
                    null,
                  ]
                : [
                    modal.item?.serviceTypeId ?? null,
                    saved?.serviceTypeId ?? null,
                  ],
            );
            if (saved?.serviceTypeId != null)
              openGroup(saved.serviceTypeId, "services", false);
          }}
        />
      )}
      {modal?.type === "category" && (
        <CategoryFormModal
          isOpen
          onClose={closeModal}
          item={modal.item}
          viewOnly={modal.view}
          initialParentId={modal.parent}
          onSuccess={(saved) => {
            if (saved)
              pendingTreeGroup.current.products = {
                id: saved.categoryId,
                revision: metaRevision.products + 1,
              };
            invalidate("products", [
              modal.item?.categoryId ?? null,
              saved?.categoryId ?? null,
            ]);
            if (saved)
              setExpanded(
                (current) =>
                  new Set([
                    ...current,
                    catalogKey("products", saved.categoryId),
                    ...(saved.categoryParentId
                      ? ancestorKeys(
                          metadata.products?.value.groups ?? [],
                          saved.categoryParentId,
                          "products",
                        )
                      : []),
                  ]),
              );
          }}
        />
      )}
      {modal?.type === "service-type" && (
        <ServiceTypeFormModal
          isOpen
          onClose={closeModal}
          item={modal.item}
          viewOnly={modal.view}
          onSuccess={(saved) => {
            if (saved)
              pendingTreeGroup.current.services = {
                id: saved.serviceTypeId,
                revision: metaRevision.services + 1,
              };
            invalidate("services", [saved?.serviceTypeId ?? null]);
            if (saved)
              setExpanded(
                (current) =>
                  new Set([
                    ...current,
                    catalogKey("services", saved.serviceTypeId),
                  ]),
              );
          }}
        />
      )}
      <ConfirmDialog
        isOpen={!!confirm}
        onClose={() => {
          if (!confirmBusy) setConfirm(null);
        }}
        onConfirm={() => void runConfirm()}
        title={confirm?.title ?? "Xác nhận"}
        description={confirm?.description ?? ""}
        isLoading={confirmBusy}
        variant={confirm?.destructive ? "destructive" : "default"}
      />
    </>
  );
}
