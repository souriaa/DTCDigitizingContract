import axiosClient from "@/lib/axios-interceptor";
import type { ContractItemType } from "@/services/contract-api";

export interface CatalogPickerFilter {
  page?: number;
  pageSize?: number;
  keyword?: string;
  itemType?: ContractItemType;
  selectedOnly?: boolean;
  productIds?: number[];
  serviceIds?: number[];
  focusItemType?: ContractItemType;
  focusItemId?: number;
}

export interface CatalogPickerItem {
  sourceId: number;
  itemType: ContractItemType;
  name: string | null;
  nameEn: string | null;
  unitName: string | null;
  unitNameEn: string | null;
  price: number | null;
}

export interface CatalogPickerPage {
  items: CatalogPickerItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export const catalogPickerApi = {
  search: (filter: CatalogPickerFilter = {}, signal?: AbortSignal) =>
    axiosClient.post<unknown, CatalogPickerPage>("/api/catalog/workspace/items/search", filter, { signal }),
};
