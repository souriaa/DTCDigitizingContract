"use client";
import { useEffect, useState } from "react";
import {
  Eye,
  MoreHorizontal,
  Pencil,
  Lock,
  Unlock,
  RotateCcw,
  Trash2,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Badge } from "@/components/ui/badge";
import { Alert, AlertTitle, AlertDescription } from "@/components/ui/alert";
import { Card, CardHeader, CardContent } from "@/components/ui/card";
import { Empty, EmptyDescription } from "@/components/ui/empty";
import { Spinner } from "@/components/ui/spinner";
import { SplitActionMenu } from "@/components/ui/custom/split-action-menu";
import {
  Select,
  SelectTrigger,
  SelectValue,
  SelectContent,
  SelectItem,
} from "@/components/ui/select";
import {
  Pagination,
  PaginationContent,
  PaginationItem,
} from "@/components/ui/pagination";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { formatCurrency } from "@/lib/format-currency";
import { format } from "date-fns";
import { getApiErrorMessage } from "@/lib/api-error";
import {
  workspaceApi,
  type CatalogKind,
  type CatalogItem,
  type CatalogPage,
  type CatalogListFilter,
} from "@/services/catalog/workspace-api";
import { getProductStatusLabel } from "@/services/catalog/products-api";
import { getServiceStatusLabel } from "@/services/catalog/services-api";
import { catalogQueryKey, type createCatalogQueue } from "./catalog-state";

export type ItemAction = "view" | "edit" | "toggle" | "delete" | "restore";
export interface CatalogPageState {
  page: number;
  pageSize: number;
}
export function CatalogItemList({
  kind,
  filter,
  active,
  scope,
  revision,
  cache,
  queue,
  pagination,
  onPage,
  canManage,
  onAction,
  onBulkDelete,
  onOpenGroup,
}: {
  kind: CatalogKind;
  filter: CatalogListFilter;
  active: boolean;
  scope: string;
  revision: number;
  cache: Map<string, { revision: number; data: CatalogPage }>;
  queue: ReturnType<typeof createCatalogQueue>;
  pagination: CatalogPageState;
  onPage: (value: CatalogPageState) => void;
  canManage: boolean;
  onAction: (item: CatalogItem, action: ItemAction) => void;
  onBulkDelete: (items: CatalogItem[]) => void;
  onOpenGroup?: (id: number | null) => void;
}) {
  const key = catalogQueryKey(scope, kind, filter);
  const [result, setResult] = useState<{
    key: string;
    revision: number;
    data: CatalogPage;
  } | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [attempt, setAttempt] = useState(0);
  // Selection belongs to the exact query/revision, never to hidden rows or another group.
  const selectionScope = `${key}:${revision}`;
  const [selection, setSelection] = useState<{
    scope: string;
    ids: Set<number>;
  }>({ scope: "", ids: new Set() });
  const selected =
    selection.scope === selectionScope ? selection.ids : new Set<number>();
  useEffect(() => {
    if (!active) return;
    const controller = new AbortController();
    const load = async () => {
      const cached = cache.get(key);
      if (cached?.revision === revision && attempt === 0) {
        setResult({ key, revision, data: cached.data });
        setError("");
        setLoading(false);
        return;
      }
      setLoading(true);
      setError("");
      try {
        const data = await queue.run(
          () => workspaceApi.items(kind, filter, controller.signal),
          controller.signal,
        );
        if (controller.signal.aborted) return;
        const lastPage = Math.max(
          1,
          Math.ceil(data.totalCount / pagination.pageSize),
        );
        if (pagination.page > lastPage) {
          onPage({ ...pagination, page: lastPage });
          return;
        }
        if (cache.size >= 200) cache.delete(cache.keys().next().value!);
        cache.set(key, { revision, data });
        setResult({ key, revision, data });
      } catch (requestError) {
        if (!controller.signal.aborted)
          setError(
            getApiErrorMessage(
              requestError,
              "Không thể tải mặt hàng trong nhóm này.",
            ),
          );
      } finally {
        if (!controller.signal.aborted) setLoading(false);
      }
    };
    void load();
    return () => controller.abort();
    // key represents every query input; callbacks and filter are reconstructed on each render.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key, active, revision, attempt, cache, queue]);
  const data =
    result?.key === key && result.revision === revision ? result.data : null;
  const items = data?.items ?? [];
  const isArchive = filter.status === 2;
  const actions = (item: CatalogItem) => (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="ghost"
          size="icon"
          aria-label={`Thao tác ${item.name}`}
        >
          <MoreHorizontal className="size-4" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuItem onSelect={() => onAction(item, "view")}>
          <Eye />
          Xem chi tiết
        </DropdownMenuItem>
        {canManage &&
          (item.status === 2 ? (
            <DropdownMenuItem onSelect={() => onAction(item, "restore")}>
              <RotateCcw />
              Khôi phục
            </DropdownMenuItem>
          ) : (
            <>
              <DropdownMenuItem onSelect={() => onAction(item, "edit")}>
                <Pencil />
                Chỉnh sửa
              </DropdownMenuItem>
              <DropdownMenuItem onSelect={() => onAction(item, "toggle")}>
                {item.status === 1 ? <Lock /> : <Unlock />}
                {item.status === 1 ? "Ngừng hoạt động" : "Mở hoạt động"}
              </DropdownMenuItem>
              <DropdownMenuItem
                className="text-destructive"
                onSelect={() => onAction(item, "delete")}
              >
                <Trash2 />
                Xóa
              </DropdownMenuItem>
            </>
          ))}
      </DropdownMenuContent>
    </DropdownMenu>
  );
  const toggle = (id: number) =>
    setSelection((current) => {
      const ids = new Set(current.scope === selectionScope ? current.ids : []);
      if (ids.has(id)) ids.delete(id);
      else ids.add(id);
      return { scope: selectionScope, ids };
    });
  return (
    <div className="min-w-0 space-y-3">
      {loading && (
        <Spinner
          text="Đang tải mặt hàng..."
          className="flex-row justify-start gap-2"
          iconClassName="size-4"
        />
      )}
      {error ? (
        <Alert variant="destructive">
          <AlertTitle>Không thể tải mặt hàng</AlertTitle>
          <AlertDescription>
            {error}
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => setAttempt((value) => value + 1)}
            >
              Thử lại
            </Button>
          </AlertDescription>
        </Alert>
      ) : (
        <>
          {canManage && !isArchive && items.length > 0 && (
            <div className="flex items-center gap-3 text-sm">
              <Checkbox
                aria-label="Chọn các mặt hàng trên trang hiện tại"
                checked={selected.size === items.length}
                onCheckedChange={(checked) =>
                  setSelection({
                    scope: selectionScope,
                    ids: new Set(checked ? items.map((item) => item.id) : []),
                  })
                }
              />
              <div className="py-2">Chọn trang hiện tại</div>
              {selected.size > 0 && (
                <SplitActionMenu
                  variant="destructive"
                  primaryLabel={`Xóa ${selected.size} mục đã chọn`}
                  primaryIcon={<Trash2 className="h-4 w-4" />}
                  onPrimaryClick={() =>
                    onBulkDelete(items.filter((item) => selected.has(item.id)))
                  }
                  menuItems={[]}
                />
              )}
            </div>
          )}
          {!loading && items.length === 0 && (
            <Empty className="py-3 md:py-3">
              <EmptyDescription>
                Chưa có mặt hàng phù hợp với bộ lọc.
              </EmptyDescription>
            </Empty>
          )}
          {items.length > 0 && (
            <>
              <div className="hidden overflow-x-auto rounded-lg border md:block">
                <Table>
                  <TableHeader>
                    <TableRow>
                      {canManage && !isArchive && (
                        <TableHead className="w-10" />
                      )}
                      <TableHead>
                        Tên {kind === "products" ? "sản phẩm" : "dịch vụ"}
                      </TableHead>
                      <TableHead className="text-right">Đơn giá</TableHead>
                      {kind === "services" && (
                        <>
                          <TableHead className="text-right">
                            Phí thiết lập
                          </TableHead>
                          <TableHead className="text-right">
                            Phí duy trì
                          </TableHead>
                          <TableHead>Dịch vụ cha</TableHead>
                        </>
                      )}
                      <TableHead>Trạng thái</TableHead>
                      <TableHead>Ngày tạo</TableHead>
                      <TableHead className="w-10" />
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {items.map((item) => (
                      <TableRow key={item.id}>
                        {canManage && !isArchive && (
                          <TableCell>
                            <Checkbox
                              aria-label={`Chọn ${item.name}`}
                              checked={selected.has(item.id)}
                              onCheckedChange={() => toggle(item.id)}
                            />
                          </TableCell>
                        )}
                        <TableCell>
                          <Button
                            type="button"
                            variant="link"
                            className="h-auto max-w-full justify-start p-0 text-left font-medium whitespace-normal"
                            onClick={() => onAction(item, "view")}
                          >
                            {item.name}
                          </Button>
                          {item.code && (
                            <p className="text-xs text-muted-foreground">
                              {item.code}
                            </p>
                          )}
                          {item.unitName && <p className="text-xs text-muted-foreground">Đơn vị: {item.unitName}{item.unitNameEn ? ` / ${item.unitNameEn}` : ""}</p>}
                          {onOpenGroup && (
                            <Button
                              type="button"
                              variant="link"
                              className="flex h-auto max-w-full justify-start p-0 text-left text-xs whitespace-normal"
                              onClick={() => onOpenGroup(item.groupId)}
                            >
                              {item.groupPath}
                            </Button>
                          )}
                        </TableCell>
                        <TableCell className="text-right whitespace-nowrap">
                          {formatCurrency(item.price ?? 0)}
                        </TableCell>
                        {kind === "services" && (
                          <>
                            <TableCell className="text-right whitespace-nowrap">
                              {formatCurrency(item.setupPrice ?? 0)}
                            </TableCell>
                            <TableCell className="text-right whitespace-nowrap">
                              {formatCurrency(item.maintainPrice ?? 0)}
                            </TableCell>
                            <TableCell>
                              {item.parentName ?? "Không có"}
                            </TableCell>
                          </>
                        )}
                        <TableCell>
                          <Badge
                            variant={
                              item.status === 1 ? "default" : "secondary"
                            }
                          >
                            {kind === "products"
                              ? getProductStatusLabel(item.status ?? undefined)
                              : getServiceStatusLabel(item.status)}
                          </Badge>
                        </TableCell>
                        <TableCell className="whitespace-nowrap text-xs">
                          {item.createdAt
                            ? format(new Date(item.createdAt), "dd/MM/yyyy HH:mm")
                            : "N/A"}
                        </TableCell>
                        <TableCell>{actions(item)}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
              <div className="space-y-2 md:hidden">
                {items.map((item) => (
                  <Card key={item.id} className="gap-2 py-3">
                    <CardHeader className="flex flex-row items-center gap-2 px-3">
                      {canManage && !isArchive && (
                        <Checkbox
                          aria-label={`Chọn ${item.name}`}
                          checked={selected.has(item.id)}
                          onCheckedChange={() => toggle(item.id)}
                        />
                      )}
                      <Button
                        type="button"
                        variant="link"
                        className="h-auto min-w-0 flex-1 justify-start p-0 text-left font-medium whitespace-normal"
                        onClick={() => onAction(item, "view")}
                      >
                        {item.name}
                      </Button>
                      {actions(item)}
                    </CardHeader>
                    <CardContent className="space-y-2 px-3">
                      {item.code && (
                        <p className="text-xs text-muted-foreground">
                          {item.code}
                        </p>
                      )}
                      <p>{formatCurrency(item.price ?? 0)}</p>
                      {kind === "services" && (
                        <p className="text-xs">
                          Thiết lập: {formatCurrency(item.setupPrice ?? 0)} ·
                          Duy trì: {formatCurrency(item.maintainPrice ?? 0)} ·
                          Cha: {item.parentName ?? "Không có"}
                        </p>
                      )}
                      <p className="text-xs">
                        {kind === "products"
                          ? getProductStatusLabel(item.status ?? undefined)
                          : getServiceStatusLabel(item.status)}
                      </p>
                      {onOpenGroup && (
                        <Button
                          type="button"
                          variant="link"
                          className="h-auto max-w-full justify-start p-0 text-left text-xs whitespace-normal"
                          onClick={() => onOpenGroup(item.groupId)}
                        >
                          {item.groupPath}
                        </Button>
                      )}
                    </CardContent>
                  </Card>
                ))}
              </div>
            </>
          )}
        </>
      )}
      <div className="flex flex-wrap items-center justify-between gap-2 text-xs text-muted-foreground">
        <span>
          {data?.totalCount ?? 0} mặt hàng · Trang {pagination.page}/
          {Math.max(
            1,
            Math.ceil((data?.totalCount ?? 0) / pagination.pageSize),
          )}
        </span>
        <div className="flex items-center gap-2">
          <Select
            value={String(pagination.pageSize)}
            onValueChange={(value) =>
              onPage({ page: 1, pageSize: Number(value) })
            }
          >
            <SelectTrigger size="sm" aria-label="Số dòng trong nhóm">
              <SelectValue />
            </SelectTrigger>
            <SelectContent showSearch={false}>
              {[5, 10, 20, 50, 100].map((value) => (
                <SelectItem key={value} value={String(value)}>
                  {value} dòng
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Pagination
            aria-label="Phân trang mặt hàng trong nhóm"
            className="mx-0 w-auto"
          >
            <PaginationContent>
              <PaginationItem>
                <Button
                  variant="outline"
                  size="sm"
                  aria-label="Trang trước"
                  disabled={pagination.page <= 1 || loading}
                  onClick={() =>
                    onPage({ ...pagination, page: pagination.page - 1 })
                  }
                >
                  Trước
                </Button>
              </PaginationItem>
              <PaginationItem>
                <Button
                  variant="outline"
                  size="sm"
                  aria-label="Trang sau"
                  disabled={
                    pagination.page * pagination.pageSize >=
                      (data?.totalCount ?? 0) || loading
                  }
                  onClick={() =>
                    onPage({ ...pagination, page: pagination.page + 1 })
                  }
                >
                  Sau
                </Button>
              </PaginationItem>
            </PaginationContent>
          </Pagination>
        </div>
      </div>
    </div>
  );
}
