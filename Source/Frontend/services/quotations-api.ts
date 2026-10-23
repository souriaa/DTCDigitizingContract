import axiosClient from "@/lib/axios-interceptor";

export interface QuotationItemResponse {
  productId: number;
  productName: string;
  quantity: number;
  unitPrice: number;
  amount: number;
}

export interface QuotationResponseDto {
  quotationId: number;
  quotationNo: string;
  customerId: number;
  quotationDate: string;
  totalAmount: number;
  quatationStatus: string;
  items: QuotationItemResponse[];
}

export interface CreateQuotationRequestDto {
  customerId: number;
  quotationItems: Omit<QuotationItemResponse, "amount" | "productName">[];
}

const BASE_URL = "/api/Quotation";

export const quotationApi = {
  getAll: () => {
    return axiosClient.get<unknown, QuotationResponseDto[]>(BASE_URL);
  },

  getById: (id: number) => {
    return axiosClient.get<unknown, QuotationResponseDto>(`${BASE_URL}/${id}`);
  },

  create: (data: CreateQuotationRequestDto) => {
    return axiosClient.post<unknown, QuotationResponseDto>(BASE_URL, data);
  },

  delete: (id: number) => {
    return axiosClient.delete<unknown, void>(`${BASE_URL}/${id}`);
  },
};
