"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { format } from "date-fns";
import { ArrowLeft, ChevronLeft, ChevronRight } from "lucide-react";
import { PermissionGuard, ResourceAccessErrorState } from "@/components/auth/permission-guard";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Header } from "@/components/ui/custom/header";
import { StatusBadge } from "@/components/ui/custom/status-badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { getApiErrorMessage, getResourceAccessErrorKind } from "@/lib/api-error";
import { formatCurrency } from "@/lib/format-currency";
import { RBAC_PERMISSIONS } from "@/lib/rbac";
import { customerApi, type CustomerLookupResponse } from "@/services/customers-api";
import { quotationApi, type QuotationResponseDto } from "@/services/quotations-api";

function QuotationDetailContent() {
  const { id } = useParams<{ id: string }>();
  const quotationId = Number(id);
  const validId = Number.isSafeInteger(quotationId) && quotationId > 0;
  const [quotation, setQuotation] = useState<QuotationResponseDto | null>(null);
  const [customer, setCustomer] = useState<CustomerLookupResponse | null>(null);
  const [customerUnavailable, setCustomerUnavailable] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<unknown>(null);
  const [reload, setReload] = useState(0);
  const [page, setPage] = useState(1);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    setQuotation(null);
    setCustomer(null);
    setCustomerUnavailable(false);
    setPage(1);
    if (!validId) { setLoading(false); return; }
    const load = async () => {
      try {
        const result = await quotationApi.getById(quotationId);
        if (cancelled) return;
        setQuotation(result);
        try {
          const customerResult = await customerApi.lookup();
          if (!cancelled) {
            const matched = customerResult.find((item) => item.customerId === result.customerId);
            setCustomer(matched ?? null);
            setCustomerUnavailable(!matched);
          }
        } catch {
          // Reading a quotation does not require access to customer management.
          if (!cancelled) setCustomerUnavailable(true);
        }
      } catch (cause) {
        if (!cancelled) setError(cause);
      } finally {
        if (!cancelled) setLoading(false);
      }
    };
    void load();
    return () => { cancelled = true; };
  }, [quotationId, validId, reload]);

  const totalPages = Math.max(1, Math.ceil((quotation?.items.length ?? 0) / 10));
  return <>
    <Header />
    <main className="grow overflow-y-auto p-2 lg:p-10 space-y-6">
      <Button asChild variant="ghost" size="sm"><Link href="/quotations"><ArrowLeft className="size-4" />Quay lại báo giá</Link></Button>
      {loading ? <div role="status" aria-label="Đang tải báo giá" className="space-y-4"><Skeleton className="h-10 w-64" /><Skeleton className="h-40 w-full" /></div> : !quotation ? <ResourceAccessErrorState
        kind={!validId ? "not-found" : getResourceAccessErrorKind(error)}
        description={!validId ? "Mã định danh báo giá không hợp lệ." : getApiErrorMessage(error, "Không thể tải báo giá.")}
        backHref="/quotations" backLabel="Quay lại danh sách báo giá"
        onRetry={validId ? () => setReload((value) => value + 1) : undefined}
      /> : <>
        <div className="flex flex-wrap items-center gap-3"><h1 className="text-2xl font-bold">Báo giá {quotation.quotationNo}</h1><StatusBadge status={quotation.quatationStatus} /></div>
        <div className="grid gap-4 md:grid-cols-3">
          <Card><CardHeader><CardTitle>Khách hàng</CardTitle></CardHeader><CardContent><p className="font-semibold">{customer?.customerCompany || customer?.customerFullName || (customerUnavailable ? "Không tải được thông tin khách hàng" : "Chưa có thông tin")}</p>{customer?.customerCode && <p className="mt-1 text-sm text-muted-foreground">{customer.customerCode}</p>}</CardContent></Card>
          <Card><CardHeader><CardTitle>Ngày báo giá</CardTitle></CardHeader><CardContent>{/* SQL GETDATE(): display the returned server-local clock, not assumed UTC. */}{format(new Date(quotation.quotationDate), "dd/MM/yyyy HH:mm")}</CardContent></Card>
          <Card><CardHeader><CardTitle>Tổng giá trị</CardTitle></CardHeader><CardContent className="text-xl font-semibold text-primary">{formatCurrency(quotation.totalAmount)}</CardContent></Card>
        </div>
        <Card><CardHeader><CardTitle>Sản phẩm ({quotation.items.length})</CardTitle></CardHeader><CardContent className="space-y-4">
          <Table><TableHeader><TableRow><TableHead>Sản phẩm</TableHead><TableHead className="text-right">Số lượng</TableHead><TableHead className="text-right">Đơn giá</TableHead><TableHead className="text-right">Thành tiền</TableHead></TableRow></TableHeader><TableBody>
            {!quotation.items.length && <TableRow><TableCell colSpan={4} className="text-center text-muted-foreground">Không có sản phẩm.</TableCell></TableRow>}
            {quotation.items.slice((page - 1) * 10, page * 10).map((item, index) => <TableRow key={`${item.productId}-${(page - 1) * 10 + index}`}><TableCell className="whitespace-normal font-medium">{item.productName || "Sản phẩm chưa có tên"}</TableCell><TableCell className="text-right">{item.quantity}</TableCell><TableCell className="text-right">{formatCurrency(item.unitPrice)}</TableCell><TableCell className="text-right font-semibold">{formatCurrency(item.amount)}</TableCell></TableRow>)}
          </TableBody></Table>
          {totalPages > 1 && <div className="flex items-center justify-end gap-2"><Button type="button" variant="outline" size="sm" disabled={page <= 1} onClick={() => setPage((value) => value - 1)}><ChevronLeft className="size-4" />Trước</Button><span className="text-sm">Trang {page}/{totalPages}</span><Button type="button" variant="outline" size="sm" disabled={page >= totalPages} onClick={() => setPage((value) => value + 1)}>Sau<ChevronRight className="size-4" /></Button></div>}
        </CardContent></Card>
      </>}
    </main>
  </>;
}

export default function QuotationDetailPage() {
  return <PermissionGuard permission={RBAC_PERMISSIONS.quotationManage}><QuotationDetailContent /></PermissionGuard>;
}
