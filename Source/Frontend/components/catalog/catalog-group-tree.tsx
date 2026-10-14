"use client";
import type { ReactNode } from "react";
import type {
  CatalogGroup,
  CatalogKind,
} from "@/services/catalog/workspace-api";
import { CatalogGroupPanel } from "./catalog-group-panel";
import { catalogKey, catalogRootGroups } from "./catalog-state";
import { Button } from "@/components/ui/button";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Pagination, PaginationContent, PaginationItem } from "@/components/ui/pagination";

interface TreeProps {
  kind: CatalogKind;
  nodes: CatalogGroup[];
  expanded: Set<string>;
  active: boolean;
  pagination: { page: number; pageSize: number };
  onPaginationChange: (value: { page: number; pageSize: number }) => void;
  onToggle: (key: string) => void;
  actions: (node: CatalogGroup) => ReactNode;
  list: (node: CatalogGroup, enabled: boolean) => ReactNode;
}
function Branch({
  node,
  ancestorsOpen,
  depth,
  props,
}: {
  node: CatalogGroup;
  ancestorsOpen: boolean;
  depth: number;
  props: TreeProps;
}) {
  const open = props.expanded.has(catalogKey(props.kind, node.id));
  const children =
    props.kind === "products"
      ? props.nodes.filter((item) => item.parentId === node.id)
      : [];
  const enabled = ancestorsOpen && open && props.active;
  return (
    <div
      id={`catalog-${props.kind}-${node.id}`}
      tabIndex={-1}
      className="min-w-0 scroll-mt-6"
    >
      <CatalogGroupPanel
        name={node.name}
        path={node.path}
        count={node.directCount}
        subtreeCount={node.subtreeCount}
        open={open}
        onToggle={() => props.onToggle(catalogKey(props.kind, node.id))}
        actions={props.actions(node)}
      >
        {props.list(node, enabled)}
        {children.length > 0 && (
          <div
            className={`space-y-3 border-l ${depth < 3 ? "ml-1 pl-2 sm:ml-2 sm:pl-3" : "pl-1"}`}
          >
            {children.map((child) => (
              <Branch
                key={child.id}
                node={child}
                ancestorsOpen={enabled}
                depth={depth + 1}
                props={props}
              />
            ))}
          </div>
        )}
      </CatalogGroupPanel>
    </div>
  );
}
export function CatalogGroupTree(props: TreeProps) {
  const roots = catalogRootGroups(props.nodes, props.kind);
  const { pageSize } = props.pagination;
  const totalPages = Math.max(1, Math.ceil(roots.length / pageSize));
  const page = Math.min(props.pagination.page, totalPages);
  const visibleRoots = roots.slice((page - 1) * pageSize, page * pageSize);
  const groupLabel = props.kind === "products" ? "danh mục gốc" : "loại dịch vụ";
  return (
    <div className="space-y-3">
      {visibleRoots.map((node) => (
          <Branch
            key={node.id}
            node={node}
            ancestorsOpen={props.active}
            depth={0}
            props={props}
          />
        ))}
      {roots.length > 0 && (
        <div className="flex flex-wrap items-center justify-between gap-2 border-t pt-3 text-xs text-muted-foreground">
          <span>{roots.length} {groupLabel} · Trang {page}/{totalPages}</span>
          <div className="flex flex-wrap items-center gap-2">
            <Select
              value={String(pageSize)}
              onValueChange={(value) => props.onPaginationChange({ page: 1, pageSize: Number(value) })}
            >
              <SelectTrigger size="sm" aria-label={`Số ${groupLabel} mỗi trang`}>
                <SelectValue />
              </SelectTrigger>
              <SelectContent showSearch={false}>
                {[5, 10, 20, 50].map((size) => (
                  <SelectItem key={size} value={String(size)}>{size} {groupLabel}/trang</SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Pagination className="mx-0 w-auto" aria-label={`Phân trang ${groupLabel}`}>
              <PaginationContent>
                <PaginationItem>
                  <Button variant="outline" size="sm" disabled={page <= 1}
                    onClick={() => props.onPaginationChange({ page: page - 1, pageSize })}>
                    Trước
                  </Button>
                </PaginationItem>
                <PaginationItem>
                  <Button variant="outline" size="sm" disabled={page >= totalPages}
                    onClick={() => props.onPaginationChange({ page: page + 1, pageSize })}>
                    Sau
                  </Button>
                </PaginationItem>
              </PaginationContent>
            </Pagination>
          </div>
        </div>
      )}
    </div>
  );
}
