import axiosClient from "@/lib/axios-interceptor";
import type { ProductResponse } from "./products-api";
import type { ServiceResponse } from "./services-api";

export type CatalogKind = "products" | "services";
export interface CatalogFilter { status?: number; fromDate?: string; toDate?: string; keyword?: string; }
export interface CatalogGroup { id: number; name: string; parentId: number | null; order: number | null; directCount: number; subtreeCount: number; descendantCount: number; path: string; }
export interface CatalogGroups { groups: CatalogGroup[]; unassignedCount: number; totalCount: number; }
export interface CatalogItem { id: number; name: string | null; unitName: string | null; unitNameEn: string | null; code: string | null; groupId: number | null; groupPath: string; parentId: number | null; parentName: string | null; price: number | null; setupPrice: number | null; maintainPrice: number | null; status: number | null; createdAt: string | null; }
export interface CatalogPage { items: CatalogItem[]; totalCount: number; page: number; pageSize: number; totalPages: number; }
export interface CatalogListFilter extends CatalogFilter { page: number; pageSize: number; groupId?: number; unassignedOnly?: boolean; }
const base = "/api/catalog/workspace";
export const workspaceApi = {
  groups: (kind: CatalogKind, params: CatalogFilter, signal: AbortSignal) => axiosClient.get<unknown, CatalogGroups>(`${base}/${kind === "products" ? "categories/tree" : "service-types/groups"}`, { params, signal }),
  items: (kind: CatalogKind, params: CatalogListFilter, signal: AbortSignal) => axiosClient.get<unknown, CatalogPage>(`${base}/${kind}`, { params, signal }),
  product: (id: number) => axiosClient.get<unknown, ProductResponse>(`${base}/products/${id}`),
  service: (id: number) => axiosClient.get<unknown, ServiceResponse>(`${base}/services/${id}`),
};
