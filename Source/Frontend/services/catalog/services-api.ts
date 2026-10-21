import axiosClient from "@/lib/axios-interceptor";

export enum ServiceStatus {
  Active = 1,
  Inactive = 0,
  Deleted = 2,
}

export const getServiceStatusLabel = (status?: number | null) => {
  switch (status) {
    case ServiceStatus.Active:
      return "Đang hoạt động";
    case ServiceStatus.Inactive:
      return "Ngừng hoạt động";
    case ServiceStatus.Deleted:
      return "Đã xóa";
    default:
      return "Chưa cập nhật";
  }
};

export interface ServiceFilterParams {
  page?: number;
  pageSize?: number;
  keyword?: string | null;
  serviceTypeId?: number | null;
  status?: number | null;
  langId?: number | null;
  fromDate?: string | null;
  toDate?: string | null;
}

export interface CreateServiceRequest {
  unitName: string;
  unitNameEn: string;
  serviceName: string;
  serviceNameEn: string;
  serviceTypeId: number;
  serviceParentId?: number | null;
  servicePrice?: number | null;
  setupPrice?: number | null;
  maintainPrice?: number | null;
  langId?: number | null;
  serviceImageIcon?: string | null;
  serviceShortDesc?: string | null;
  serviceContent?: string | null;
  serviceOrder?: number | null;
  serviceRegion?: number | null;
  rewrite?: string | null;
  titleBrowser?: string | null;
  metaKeyword?: string | null;
  metaDescription?: string | null;
  others?: string | null;
}

export interface UpdateServiceRequest {
  unitName: string;
  unitNameEn: string;
  serviceName: string;
  serviceNameEn: string;
  serviceTypeId: number;
  serviceParentId?: number | null;
  servicePrice?: number | null;
  setupPrice?: number | null;
  maintainPrice?: number | null;
  langId?: number | null;
  serviceImageIcon?: string | null;
  serviceShortDesc?: string | null;
  serviceContent?: string | null;
  serviceOrder?: number | null;
  serviceRegion?: number | null;
  rewrite?: string | null;
  titleBrowser?: string | null;
  metaKeyword?: string | null;
  metaDescription?: string | null;
  others?: string | null;
}

export interface ServiceResponse {
  unitName: string | null;
  unitNameEn: string | null;
  serviceId: number;
  serviceName?: string | null;
  serviceNameEn: string | null;
  serviceTypeId?: number | null;
  serviceTypeName?: string | null;
  serviceParentId?: number | null;
  servicePrice?: number | null;
  setupPrice?: number | null;
  maintainPrice?: number | null;
  status?: number | null;
  langId?: number | null;
  serviceImageIcon?: string | null;
  serviceShortDesc?: string | null;
  serviceContent?: string | null;
  serviceOrder?: number | null;
  serviceRegion?: number | null;
  rewrite?: string | null;
  titleBrowser?: string | null;
  metaKeyword?: string | null;
  metaDescription?: string | null;
  others?: string | null;
  userCreated?: number | null;
  userModified?: number | null;
  dateCreated?: string | null;
  dateModified?: string | null;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

const BASE_URL = "/api/catalog/services";

export interface ServiceOptionResponse {
  serviceId: number;
  serviceName?: string | null;
  serviceParentId?: number | null;
  status?: number | null;
}

export const serviceApi = {
  getOptions: (signal?: AbortSignal) => {
    return axiosClient.get<unknown, ServiceOptionResponse[]>(`${BASE_URL}/options`, { signal });
  },
  getList: (params: ServiceFilterParams, signal?: AbortSignal) => {
    return axiosClient.get<any, PagedResult<ServiceResponse>>(BASE_URL, {
      params,
      signal,
    });
  },
  getById: (id: number) => {
    return axiosClient.get<any, ServiceResponse>(`${BASE_URL}/${id}`);
  },
  create: (data: CreateServiceRequest) => {
    return axiosClient.post<any, ServiceResponse>(BASE_URL, data);
  },
  update: (id: number, data: UpdateServiceRequest) => {
    return axiosClient.put<any, { serviceId: number }>(`${BASE_URL}/${id}`, data);
  },
  setStatus: (id: number, status: number) => {
    return axiosClient.patch<any, { serviceId: number; status: number }>(
      `${BASE_URL}/${id}/status`,
      null,
      { params: { status } },
    );
  },
  delete: (id: number) => {
    return axiosClient.delete<any, { serviceId: number }>(`${BASE_URL}/${id}`);
  },
  restore: (id: number) => {
    return axiosClient.post<unknown, { serviceId: number; status: number }>(`${BASE_URL}/${id}/restore`);
  },
};
