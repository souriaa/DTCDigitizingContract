"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import Link from "next/link";
import { isAxiosError } from "axios";
import { Trash2 } from "lucide-react";
import { Header } from "@/components/ui/custom/header";
import { Button } from "@/components/ui/button";
import { StatusBadge } from "@/components/ui/custom/status-badge";
import { ConfirmDialog } from "@/components/ui/custom/confirm-dialog";
import { PermissionGuard } from "@/components/auth/permission-guard";
import { RBAC_PERMISSIONS } from "@/lib/rbac";
import { formatCurrency } from "@/lib/format-currency";
import { formatDateTime } from "@/lib/format-date-time";
import { quotationApi, type QuotationResponseDto } from "@/services/quotations-api";
import { customerApi, type CustomerResponse } from "@/services/customers-api";
import { toast } from "@/components/ui/sonner";

function QuotationDetailContent() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const [quotation, setQuotation] = useState<QuotationResponseDto | null>(null);
  const [customer, setCustomer] = useState<CustomerResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [page, setPage] = useState(1);
  useEffect(() => {
    let cancelled = false;
    async function load() {
      try {
        const data = await quotationApi.getById(Number(id));
        if (cancelled) return;
        setQuotation(data);
        try { const customerData = await customerApi.getById(data.customerId); if (!cancelled) setCustomer(customerData); } catch { /* The quotation remains readable without customer-management access. */ }
      } catch (cause) {
        if (!cancelled) setError(isAxiosError(cause) && typeof cause.response?.data?.message === "string" ? cause.response.data.message : "Không thể tải báo giá.");
      } finally { if (!cancelled) setLoading(false); }
    }
    void load();
    return () => { cancelled = true; };
  }, [id]);
  const remove = async () => {
    if (!quotation || deleting) return;
    setDeleting(true);
    try { await quotationApi.delete(quotation.quotationId); toast.success("Xóa báo giá thành công"); router.push("/quotations"); }
    catch (cause) { toast.error(isAxiosError(cause) ? cause.response?.data?.message || "Không thể xóa báo giá." : "Không thể xóa báo giá."); setDeleting(false); }
  };
  const items = quotation?.items || [];
  const pages = Math.max(1, Math.ceil(items.length / 5));
  return <><Header /><main className="grow overflow-y-auto p-4 lg:p-10 space-y-6">
    <Link href="/quotations" className="text-sm text-primary">← Quay lại danh sách báo giá</Link>
    {loading ? <p role="status">Đang tải báo giá...</p> : !quotation ? <p role="alert" className="text-destructive">{error}</p> : <>
      <div className="flex flex-wrap items-center justify-between gap-4"><div className="flex flex-wrap items-center gap-3"><h1 className="text-2xl font-bold">{quotation.quotationNo}</h1><StatusBadge status={quotation.quatationStatus} /></div>{quotation.quatationStatus === "Draft" && <Button variant="destructive" onClick={() => setConfirmDelete(true)}><Trash2 className="mr-2 h-4 w-4" />Xóa bản nháp</Button>}</div>
      <section className="grid gap-4 sm:grid-cols-3">
        <div className="rounded-xl border bg-card p-5"><p className="text-sm text-muted-foreground">Khách hàng</p><p className="mt-2 font-semibold">{customer?.customerCompany || customer?.customerFullName || `KH-${quotation.customerId}`}</p></div>
        <div className="rounded-xl border bg-card p-5"><p className="text-sm text-muted-foreground">Ngày báo giá</p><p className="mt-2 font-semibold">{formatDateTime(quotation.quotationDate)}</p></div>
        <div className="rounded-xl border bg-card p-5"><p className="text-sm text-muted-foreground">Tổng giá trị</p><p className="mt-2 font-semibold text-primary">{formatCurrency(quotation.totalAmount)}</p></div>
      </section>
      <section className="rounded-xl border bg-card p-5 space-y-4"><h2 className="font-semibold">Sản phẩm ({items.length})</h2>
        {items.slice((page - 1) * 5, page * 5).map((item, index) => <div key={`${item.productId}-${index}`} className="rounded-xl border p-4"><h3 className="font-medium">{item.productName || `SP-${item.productId}`}</h3><dl className="mt-3 grid gap-4 sm:grid-cols-3"><div><dt className="text-sm text-muted-foreground">Số lượng</dt><dd>{item.quantity}</dd></div><div><dt className="text-sm text-muted-foreground">Đơn giá</dt><dd>{formatCurrency(item.unitPrice)}</dd></div><div><dt className="text-sm text-muted-foreground">Thành tiền</dt><dd className="font-semibold text-primary">{formatCurrency(item.amount)}</dd></div></dl></div>)}
        {!items.length && <p className="text-muted-foreground">Không có sản phẩm.</p>}
        {pages > 1 && <div className="flex justify-end items-center gap-3"><Button variant="outline" disabled={page <= 1} onClick={() => setPage(page - 1)}>Trước</Button><span>Trang {page}/{pages}</span><Button variant="outline" disabled={page >= pages} onClick={() => setPage(page + 1)}>Sau</Button></div>}
      </section>
      <ConfirmDialog isOpen={confirmDelete} onClose={() => !deleting && setConfirmDelete(false)} onConfirm={remove} title="Xóa báo giá bản nháp" description="Báo giá và các sản phẩm trong báo giá sẽ bị xóa. Bạn có chắc chắn tiếp tục?" confirmText="Xóa báo giá" variant="destructive" isLoading={deleting} />
    </>}
  </main></>;
}

export default function QuotationDetailPage() {
  return <PermissionGuard permission={RBAC_PERMISSIONS.quotationManage}><QuotationDetailContent /></PermissionGuard>;
}
